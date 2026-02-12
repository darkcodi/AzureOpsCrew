using System.ClientModel;
using Azure.AI.OpenAI;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.AgentManagements;
using AzureOpsCrew.Infrastructure.Ai.AgentManagements;
using AzureOpsCrew.Infrastructure.Ai.AgentManagements.Microsoft;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AzureOpsCrew.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCosmosSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
        {
            services.Configure<CosmosSettings>(configuration.GetSection(configurationKey));
            services.AddOptions<CosmosSettings>()
                .Validate(settings =>
                {
                    return !string.IsNullOrEmpty(settings.AccountEndpoint) &&
                           !string.IsNullOrEmpty(settings.AccountKey) &&
                           !string.IsNullOrEmpty(settings.DatabaseName);
                }, "Cosmos DB settings are not properly configured. Please double-check the configuration.")
                .ValidateOnStart();
            return services;
        }

        public static IServiceCollection AddAiSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
        {
            services.Configure<AiSettings>(configuration.GetSection(configurationKey));
            services.AddOptions<AiSettings>()
                .Validate(settings => settings.IsValid(), 
                    "AI settings are not properly configured. Please double-check the configuration.")
                .ValidateOnStart();
            return services;
        }

        public static void AddEFCoreCosmosDb(this IServiceCollection services)
        {
            services.AddDbContext<AzureOpsCrewContext>((sp, options) =>
            {
                var cosmosSettings = sp.GetRequiredService<IOptions<CosmosSettings>>().Value;
                options.UseCosmos(
                    cosmosSettings.AccountEndpoint!,
                    cosmosSettings.AccountKey!,
                    cosmosSettings.DatabaseName!,
                    cosmosOptions =>
                    {
                        if (cosmosSettings.DisableSslValidation)
                        {
                            cosmosOptions.HttpClientFactory(() =>
                            {
                                var handler = new HttpClientHandler();
                                handler.ServerCertificateCustomValidationCallback =
                                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                                return new HttpClient(handler);
                            });
                        }

                        if (Enum.TryParse<Microsoft.Azure.Cosmos.ConnectionMode>(cosmosSettings.ConnectionMode, true, out var mode))
                        {
                            cosmosOptions.ConnectionMode(mode);
                        }
                    });
            });
        }

        public static void AddIChatClient(this IServiceCollection services)
        {
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
}
