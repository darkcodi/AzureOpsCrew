using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.ProviderServices;
using AzureOpsCrew.Infrastructure.Db;
using AzureOpsCrew.Infrastructure.Db.Migrations;
using FluentMigrator.Runner;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Worker.Settings;

namespace Worker.Extensions;

public static class ServiceCollectionExtensions
{
    public static SqlServerSettings AddSqlServerSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        var settings = configuration.GetSection(configurationKey).Get<SqlServerSettings>() ?? new SqlServerSettings();

        // Validate the settings immediately and throw an exception if invalid
        if (string.IsNullOrEmpty(settings.ConnectionString))
            throw new InvalidOperationException($"{configurationKey}__ConnectionString is required.");

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
}
