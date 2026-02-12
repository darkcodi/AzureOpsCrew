using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddEFCoreCosmosDb(this IServiceCollection services, IConfiguration configuration, string configurationKey)
        {
            var disableSslValidation = configuration.GetValue<bool>($"{configurationKey}:DisableSslValidation");
            var connectionMode = configuration.GetValue<string>($"{configurationKey}:ConnectionMode");

            services.AddDbContext<AzureOpsCrewContext>(options =>
                options.UseCosmos(
                    configuration[$"{configurationKey}:AccountEndpoint"]!,
                    configuration[$"{configurationKey}:AccountKey"]!,
                    configuration[$"{configurationKey}:DatabaseName"]!,
                    cosmosOptions =>
                    {
                        if (disableSslValidation)
                        {
                            cosmosOptions.HttpClientFactory(() =>
                            {
                                var handler = new HttpClientHandler();
                                handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                                return new HttpClient(handler);
                            });
                        }

                        if (Enum.TryParse<Microsoft.Azure.Cosmos.ConnectionMode>(connectionMode, true, out var mode))
                        {
                            cosmosOptions.ConnectionMode(mode);
                        }
                    }));
        }
    }
}
