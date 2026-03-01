using System.Text;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using AzureOpsCrew.Infrastructure.Db.Migrations;
using Chat.Auth;
using Chat.Settings;
using FluentMigrator.Runner;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace Chat.Extensions;

public static class ServiceCollectionExtensions
{
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

        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
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
            throw new InvalidOperationException($"Unknown DB provider '{provider}'. Supported providers: SqlServer");
        }
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
}
