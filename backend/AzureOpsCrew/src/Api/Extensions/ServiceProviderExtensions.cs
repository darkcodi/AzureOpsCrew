using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AzureOpsCrew.Api.Extensions;

public static class ServiceProviderExtensions
{
    public static void RunMigrations(this IServiceProvider provider)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Log.Information("Starting database migrations...");
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
            context.Database.Migrate();
        }
        stopwatch.Stop();
        Log.Information("Database migrations completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
    }
}
