using Azure.AI.OpenAI;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.AgentManagements;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Ai.AgentManagements;
using AzureOpsCrew.Infrastructure.Ai.AgentManagements.Microsoft;
using AzureOpsCrew.Infrastructure.Ai.Providers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;
using Serilog;

namespace AzureOpsCrew.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAiSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        services.Configure<AiSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<AiSettings>()
            .Validate(settings => settings.IsValid(),
                "AI settings are not properly configured. Please double-check the configuration.")
            .ValidateOnStart();
        return services;
    }

    public static IServiceCollection AddSQLiteSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        services.Configure<SQLiteSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<SQLiteSettings>()
            .Validate(settings =>
            {
                return !string.IsNullOrEmpty(settings.DataSource);
            }, "SQLite settings are not properly configured. Please double-check the configuration.")
            .ValidateOnStart();
        return services;
    }

    public static void AddEFCoreSqliteDb(this IServiceCollection services)
    {
        services.AddDbContext<AzureOpsCrewContext>((sp, options) =>
        {
            var sqliteSettings = sp.GetRequiredService<IOptions<SQLiteSettings>>().Value;
            options.UseSqlite(sqliteSettings.DataSource!);
        });
    }

    public static IServiceCollection AddSqlServerSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        services.Configure<SqlServerSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<SqlServerSettings>()
            .Validate(settings => !string.IsNullOrEmpty(settings.ConnectionString),
                "SQL Server settings are not properly configured. Please double-check the configuration.")
            .ValidateOnStart();
        return services;
    }

    public static void AddEFCoreSqlServerDb(this IServiceCollection services)
    {
        services.AddDbContext<AzureOpsCrewContext>((sp, options) =>
        {
            var sqlServerSettings = sp.GetRequiredService<IOptions<SqlServerSettings>>().Value;
            options.UseSqlServer(sqlServerSettings.ConnectionString!);
        });
    }

    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"];
        Log.Information("Configuring database provider: {DbProvider}", provider);

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSQLiteSettings(configuration, "Sqlite");
            services.AddEFCoreSqliteDb();
        }
        else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSqlServerSettings(configuration, "SqlServer");
            services.AddEFCoreSqlServerDb();
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

    public static void AddAgentManagements(this IServiceCollection services)
    {
        services.AddTransient<IAgentFactory, AgentFactory>()
            .AddTransient<Local0AgentFactory>()
            .AddTransient<Local1AgentFactory>()
            .AddTransient<MicrosoftFoundryAgentFactory>();
    }

    public static void AddProviderServices(this IServiceCollection services)
    {
        services.AddTransient<IProviderServiceFactory, ProviderServiceFactory>()
            .AddHttpClient<OpenAIProviderService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .Services.AddHttpClient<AnthropicProviderService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .Services.AddHttpClient<OllamaProviderService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .Services.AddHttpClient<OpenRouterProviderService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            })
            .Services.AddHttpClient<AzureFoundryProviderService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            });
    }
}
