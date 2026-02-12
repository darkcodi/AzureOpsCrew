using AzureOpsCrew.Infrastructure.Db;

namespace AzureOpsCrew.Api.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static async Task RunEnsureEFCoreCosmosDbCreated(this IServiceProvider provider)
        {
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
                await context.Database.EnsureCreatedAsync();
            }
        }
    }
}
