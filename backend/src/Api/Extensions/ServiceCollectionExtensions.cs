using Azure.AI.OpenAI;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Ai.Providers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using AzureOpsCrew.Infrastructure.Db.Migrations;
using FluentMigrator.Runner;
using Serilog;

namespace AzureOpsCrew.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static AiSettings AddAiSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        var settings = configuration.GetSection(configurationKey).Get<AiSettings>() ?? new AiSettings();

        // Validate the settings immediately and throw an exception if invalid
        if (string.IsNullOrEmpty(settings.Endpoint)) throw new InvalidOperationException($"{configurationKey}__Endpoint is required.");
        if (string.IsNullOrEmpty(settings.ApiKey)) throw new InvalidOperationException($"{configurationKey}__ApiKey is required.");
        if (string.IsNullOrEmpty(settings.Model)) throw new InvalidOperationException($"{configurationKey}__Model is required.");

        services.Configure<AiSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<AiSettings>();
        return settings;
    }

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

    public static void AddIChatClient(this IServiceCollection services)
    {
        services.AddScoped<OpenAIClient>(sp =>
        {
            var aiSettings = sp.GetRequiredService<IOptions<AiSettings>>().Value;
            var options = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01);
            return new AzureOpenAIClient(
                    new Uri(aiSettings.Endpoint!),
                    new ApiKeyCredential(aiSettings.ApiKey!),
                    options);
        });
        services.AddSingleton<IChatClient>(sp =>
        {
            var aiSettings = sp.GetRequiredService<IOptions<AiSettings>>().Value;
            var options = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01);
            var chatClient = new AzureOpenAIClient(
                    new Uri(aiSettings.Endpoint!),
                    new ApiKeyCredential(aiSettings.ApiKey!),
                    options)
                .GetChatClient(aiSettings.Model!);
            return chatClient.AsIChatClient();
        });
    }

    public static void AddProviderServices(this IServiceCollection services)
    {
        services.AddTransient<IProviderServiceFactory, ProviderServiceFactory>();
        services.AddHttpClient<AnthropicProviderService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<AzureFoundryProviderService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OllamaProviderService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OpenAIProviderService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OpenRouterProviderService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }
}
