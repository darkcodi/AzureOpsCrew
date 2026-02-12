using AzureOpsCrew.Infrastructure.Db;
using Serilog;

namespace AzureOpsCrew.Api.Extensions
{
    public static class ServiceProviderExtensions
    {
        public static async Task RunEnsureEFCoreCosmosDbCreated(this IServiceProvider provider)
        {
            // Measure the time taken to ensure the database is created
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Log.Information("Starting database creation check...");
            using (var scope = provider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
                await context.Database.EnsureCreatedAsync();
            }
            stopwatch.Stop();
            Log.Information("Database creation check completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
        }
    }
}
