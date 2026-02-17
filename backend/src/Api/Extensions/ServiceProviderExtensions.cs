using FluentMigrator.Runner;
using Serilog;

namespace AzureOpsCrew.Api.Extensions;

public static class ServiceProviderExtensions
{
    public static async Task RunDbSetup(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var runner = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();

        Log.Information("Starting database migrations...");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        runner.MigrateUp();
        stopwatch.Stop();
        Log.Information("Database migrations completed in {ElapsedMilliseconds} ms", stopwatch.ElapsedMilliseconds);
    }
}
