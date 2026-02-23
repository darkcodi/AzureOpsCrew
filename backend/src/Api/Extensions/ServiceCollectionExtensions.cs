using System.Text;
using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Email;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using AzureOpsCrew.Infrastructure.Db.Migrations;
using FluentMigrator.Runner;
using Serilog;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.ProviderServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;

namespace AzureOpsCrew.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static SQLiteSettings AddSqliteSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        var settings = configuration.GetSection(configurationKey).Get<SQLiteSettings>() ?? new SQLiteSettings();

        // Validate the settings immediately and throw an exception if invalid
        if (string.IsNullOrEmpty(settings.DataSource)) throw new InvalidOperationException($"{configurationKey}__DataSource is required.");

        services.Configure<SQLiteSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<SQLiteSettings>();
        return settings;
    }

    public static SqlServerSettings AddSqlServerSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        var settings = configuration.GetSection(configurationKey).Get<SqlServerSettings>() ?? new SqlServerSettings();

        // Validate the settings immediately and throw an exception if invalid
        if (string.IsNullOrEmpty(settings.ConnectionString)) throw new InvalidOperationException($"{configurationKey}__ConnectionString is required.");

        services.Configure<SqlServerSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<SqlServerSettings>();
        return settings;
    }

    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"];
        Log.Information("Configuring database provider: {DbProvider}", provider);

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqliteSettings = services.AddSqliteSettings(configuration, "Sqlite");
            services.AddDbContext<AzureOpsCrewContext>(options =>
            {
                options.UseSqlite(sqliteSettings.DataSource!);
            });
            services.AddFluentMigratorCore()
                .ConfigureRunner(rb =>
                {
                    rb.AddSQLite()
                        .WithGlobalConnectionString(sqliteSettings.DataSource)
                        .ScanIn(typeof(M001_InitialCreate).Assembly).For.All();
                });
        }
        else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var sqlServerSettings = services.AddSqlServerSettings(configuration, "SqlServer");
            services.AddDbContext<AzureOpsCrewContext>(options =>
            {
                options.UseSqlServer(sqlServerSettings.ConnectionString!);
            });
            services.AddFluentMigratorCore()
                .ConfigureRunner(rb =>
                {
                    rb.AddSqlServer()
                        .WithGlobalConnectionString(sqlServerSettings.ConnectionString)
                        .ScanIn(typeof(M001_InitialCreate).Assembly).For.All();
                });
        }
        else
        {
            throw new InvalidOperationException($"Unknown DB provider '{provider}'. Supported providers: Sqlite, SqlServer");
        }
    }

    public static void AddProviderFacades(this IServiceCollection services)
    {
        services.AddTransient<IProviderFacadeResolver, ProviderFacadeResolver>();
        services.AddHttpClient<AnthropicProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<AzureFoundryProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OllamaProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OpenAIProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OpenRouterProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();

        if (string.IsNullOrWhiteSpace(settings.Issuer))
            throw new InvalidOperationException("Jwt__Issuer is required.");

        if (string.IsNullOrWhiteSpace(settings.Audience))
            throw new InvalidOperationException("Jwt__Audience is required.");

        if (string.IsNullOrWhiteSpace(settings.SigningKey) || settings.SigningKey.Length < 32)
            throw new InvalidOperationException("Jwt__SigningKey must be at least 32 characters.");

        if (settings.AccessTokenMinutes <= 0)
            throw new InvalidOperationException("Jwt__AccessTokenMinutes must be greater than zero.");

        if (settings.SigningKey.Contains("ChangeThisDevelopmentOnly", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A real JWT signing key must be configured.");

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddOptions<JwtSettings>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = settings.Issuer,
                ValidateAudience = true,
                ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });

        services.AddAuthorization();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
    }

    public static void AddEmailVerification(this IServiceCollection services, IConfiguration configuration)
    {
        var brevoSettings = configuration.GetSection("Brevo").Get<BrevoSettings>() ?? new BrevoSettings();
        var emailVerificationSettings = configuration.GetSection("EmailVerification").Get<EmailVerificationSettings>()
            ?? new EmailVerificationSettings();

        if (string.IsNullOrWhiteSpace(brevoSettings.ApiBaseUrl))
            throw new InvalidOperationException("Brevo__ApiBaseUrl is required.");

        if (string.IsNullOrWhiteSpace(brevoSettings.ApiKey))
            throw new InvalidOperationException("Brevo__ApiKey is required.");

        if (brevoSettings.ApiKey.Contains("CHANGEME", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A real Brevo API key must be configured.");

        if (string.IsNullOrWhiteSpace(brevoSettings.SenderEmail))
            throw new InvalidOperationException("Brevo__SenderEmail is required.");

        if (string.IsNullOrWhiteSpace(brevoSettings.SenderName))
            throw new InvalidOperationException("Brevo__SenderName is required.");

        if (emailVerificationSettings.CodeLength is < 4 or > 8)
            throw new InvalidOperationException("EmailVerification__CodeLength must be between 4 and 8.");

        if (emailVerificationSettings.CodeTtlMinutes <= 0)
            throw new InvalidOperationException("EmailVerification__CodeTtlMinutes must be greater than zero.");

        if (emailVerificationSettings.ResendCooldownSeconds < 0)
            throw new InvalidOperationException("EmailVerification__ResendCooldownSeconds must be zero or greater.");

        if (emailVerificationSettings.MaxVerificationAttempts <= 0)
            throw new InvalidOperationException("EmailVerification__MaxVerificationAttempts must be greater than zero.");

        services.Configure<BrevoSettings>(configuration.GetSection("Brevo"));
        services.AddOptions<BrevoSettings>();

        services.Configure<EmailVerificationSettings>(configuration.GetSection("EmailVerification"));
        services.AddOptions<EmailVerificationSettings>();

        services.AddHttpClient<IRegistrationEmailSender, BrevoRegistrationEmailSender>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<BrevoSettings>>().Value;
            client.BaseAddress = new Uri(settings.ApiBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddScoped<IPasswordHasher<PendingRegistration>, PasswordHasher<PendingRegistration>>();
    }
}
