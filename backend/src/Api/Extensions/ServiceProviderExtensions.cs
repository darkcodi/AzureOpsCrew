using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AzureOpsCrew.Api.Extensions;

public static class ServiceProviderExtensions
{
    public static async Task RunSqlMigrations(this IServiceProvider provider)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Log.Information("Starting database migrations...");
        using (var scope = provider.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
            await context.Database.MigrateAsync();
        }
        stopwatch.Stop();
        Log.Information("Database migrations completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
    }

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

    public static async Task RunDbSetup(this IServiceProvider provider, IConfiguration configuration)
    {
        var dbProvider = configuration["DatabaseProvider"];

        if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
            await provider.RunSqlMigrations();
        if (string.Equals(dbProvider, "CosmosDb", StringComparison.OrdinalIgnoreCase))
            await provider.RunEnsureEFCoreCosmosDbCreated();

        throw new InvalidOperationException("Db provider is not configured properly.");
    }

}
