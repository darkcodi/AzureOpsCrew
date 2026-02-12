using System.ClientModel;
using Azure.AI.OpenAI;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Serilog;

namespace AzureOpsCrew.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddCosmosSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
        {
            var cosmosSettings = new CosmosSettings
            {
                AccountEndpoint = configuration[$"{configurationKey}:AccountEndpoint"],
                AccountKey = configuration[$"{configurationKey}:AccountKey"],
                DatabaseName = configuration[$"{configurationKey}:DatabaseName"],
                DisableSslValidation = configuration.GetValue<bool>($"{configurationKey}:DisableSslValidation"),
                ConnectionMode = configuration[$"{configurationKey}:ConnectionMode"]
            };
            if (string.IsNullOrEmpty(cosmosSettings.AccountEndpoint) ||
                string.IsNullOrEmpty(cosmosSettings.AccountKey) ||
                string.IsNullOrEmpty(cosmosSettings.DatabaseName))
            {
                throw new InvalidOperationException("Cosmos DB settings are not properly configured. Please double-check the configuration.");
            }
            Log.Information(JsonConvert.SerializeObject(cosmosSettings));
            services.AddSingleton(cosmosSettings);
        }

        public static void AddAiSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
        {
            var aiSettings = new AiSettings
            {
                Endpoint = configuration[$"{configurationKey}:Endpoint"],
                ApiKey = configuration[$"{configurationKey}:ApiKey"],
                Model = configuration[$"{configurationKey}:Model"],
            };
            if (!aiSettings.IsValid())
            {
                throw new InvalidOperationException("AI settings are not properly configured. Please double-check the configuration.");
            }
            Log.Information(JsonConvert.SerializeObject(aiSettings));
            services.AddSingleton(aiSettings);
        }

        public static void AddEFCoreCosmosDb(this IServiceCollection services)
        {
            services.AddDbContext<AzureOpsCrewContext>((sp, options) =>
            {
                var cosmosSettings = sp.GetRequiredService<CosmosSettings>();
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
                var aiSettings = sp.GetRequiredService<AiSettings>();
                var chatClient = new AzureOpenAIClient(
                        new Uri(aiSettings.Endpoint!),
                        new ApiKeyCredential(aiSettings.ApiKey!),
                        new AzureOpenAIClientOptions())
                    .GetChatClient(aiSettings.Model!);
                return chatClient.AsIChatClient();
            });
        }
    }
}
