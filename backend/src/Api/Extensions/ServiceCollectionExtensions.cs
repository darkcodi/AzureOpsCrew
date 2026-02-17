using Azure.AI.OpenAI;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.AgentManagements;
using AzureOpsCrew.Infrastructure.Ai.AgentManagements;
using AzureOpsCrew.Infrastructure.Ai.AgentManagements.Microsoft;
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

    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"];
        Log.Information("Configuring database provider: {DbProvider}", provider);

        if (!string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Only SQLite is supported. Unknown DB provider '{provider}'.");

        services.AddSQLiteSettings(configuration, "Sqlite");
        services.AddEFCoreSqliteDb();
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
}
