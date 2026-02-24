using System.IdentityModel.Tokens.Jwt;
using AzureOpsCrew.Api.Auth;
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
        var keycloak = configuration.GetSection("KeycloakOidc").Get<KeycloakOidcSettings>() ?? new KeycloakOidcSettings();

        if (!keycloak.Enabled)
            throw new InvalidOperationException("KeycloakOidc__Enabled=true is required. Backend now accepts only Keycloak-issued access tokens.");

        if (string.IsNullOrWhiteSpace(keycloak.Authority))
            throw new InvalidOperationException("KeycloakOidc__Authority is required when KeycloakOidc__Enabled=true.");

        if (string.IsNullOrWhiteSpace(keycloak.ClientId))
            throw new InvalidOperationException("KeycloakOidc__ClientId is required when KeycloakOidc__Enabled=true.");

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.MapInboundClaims = false;
            options.Authority = keycloak.Authority.TrimEnd('/');
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = keycloak.Authority.TrimEnd('/'),
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = "name"
            };

            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var principal = context.Principal;
                    var azp = principal?.FindFirst("azp")?.Value ?? principal?.FindFirst("client_id")?.Value;
                    if (!string.Equals(azp, keycloak.ClientId, StringComparison.Ordinal))
                    {
                        context.Fail("Token was not issued to the expected client.");
                        return Task.CompletedTask;
                    }

                    var jwt = context.SecurityToken as JwtSecurityToken;
                    var tokenType = jwt?.Header.Typ ?? principal?.FindFirst("typ")?.Value;
                    if (string.Equals(tokenType, "ID", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Fail("ID tokens are not accepted for API authorization.");
                        return Task.CompletedTask;
                    }

                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<KeycloakAppUserSyncService>();
    }

    public static void AddKeycloakOidcSupport(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("KeycloakOidc").Get<KeycloakOidcSettings>() ?? new KeycloakOidcSettings();

        if (settings.Enabled)
        {
            if (string.IsNullOrWhiteSpace(settings.Authority))
                throw new InvalidOperationException("KeycloakOidc__Authority is required when KeycloakOidc__Enabled=true.");

            if (!Uri.TryCreate(settings.Authority, UriKind.Absolute, out var authorityUri))
                throw new InvalidOperationException("KeycloakOidc__Authority must be an absolute URL.");

            if (!string.Equals(authorityUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("KeycloakOidc__Authority must use HTTPS.");

            if (string.IsNullOrWhiteSpace(settings.ClientId))
                throw new InvalidOperationException("KeycloakOidc__ClientId is required when KeycloakOidc__Enabled=true.");
        }

        services.Configure<KeycloakOidcSettings>(configuration.GetSection("KeycloakOidc"));
        services.AddOptions<KeycloakOidcSettings>();
    }
}
