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

    public static async Task RunEnsureCreated(this IServiceProvider provider)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Log.Information("Ensuring database exists and creating if necessary...");
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
        Log.Information("Running database setup for provider: {DbProvider}", dbProvider);

        if (string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await provider.RunSqlMigrations();
        }
        else if (string.Equals(dbProvider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await provider.RunEnsureCreated();
        }
        else
        {
            throw new InvalidOperationException($"Unknown DB provider '{dbProvider}'. Supported providers: Sqlite, SqlServer");
        }
    }
}
