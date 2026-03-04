using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Cypher;
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

    public static async Task RunLongTermMemorySetup(this IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetService<CypherFactsStore>();

        if (store is not null)
        {
            Log.Information("Ensuring Neo4j schema for long-term memory...");
            await store.EnsureSchemaAsync(cancellationToken);
            Log.Information("Neo4j schema ready.");
        }
    }
}
