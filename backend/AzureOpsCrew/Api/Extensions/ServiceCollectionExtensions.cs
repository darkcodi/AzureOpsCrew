using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static void AddEFCoreCosmosDb(this IServiceCollection services, IConfiguration configuration, string configurationKey)
        {
            services.AddDbContext<AzureOpsCrewContext>(options =>
                options.UseCosmos(
                    configuration[$"{configurationKey}:AccountEndpoint"]!,
                    configuration[$"{configurationKey}:AccountKey"]!,
                    configuration[$"{configurationKey}:DatabaseName"]!));
        }
    }
}
