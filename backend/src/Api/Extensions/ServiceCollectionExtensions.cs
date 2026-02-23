using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.AgentServices;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Cypher;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory;
using AzureOpsCrew.Infrastructure.Ai.ProviderServices;
using AzureOpsCrew.Infrastructure.Db;
using AzureOpsCrew.Infrastructure.Db.Migrations;
using FluentMigrator.Runner;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AzureOpsCrew.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static SQLiteSettings AddSqliteSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        var settings = configuration.GetSection(configurationKey).Get<SQLiteSettings>() ?? new SQLiteSettings();

        // Validate the settings immediately and throw an exception if invalid
        if (string.IsNullOrEmpty(settings.DataSource)) throw new InvalidOperationException($"{configurationKey}__DataSource is required.");

        services.Configure<SQLiteSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<SQLiteSettings>();
        return settings;
    }

    public static SqlServerSettings AddSqlServerSettings(this IServiceCollection services, IConfiguration configuration, string configurationKey)
    {
        var settings = configuration.GetSection(configurationKey).Get<SqlServerSettings>() ?? new SqlServerSettings();

        // Validate the settings immediately and throw an exception if invalid
        if (string.IsNullOrEmpty(settings.ConnectionString)) throw new InvalidOperationException($"{configurationKey}__ConnectionString is required.");

        services.Configure<SqlServerSettings>(configuration.GetSection(configurationKey));
        services.AddOptions<SqlServerSettings>();
        return settings;
    }

    public static void AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["DatabaseProvider"];
        Log.Information("Configuring database provider: {DbProvider}", provider);

        if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            var sqliteSettings = services.AddSqliteSettings(configuration, "Sqlite");
            services.AddDbContext<AzureOpsCrewContext>(options =>
            {
                options.UseSqlite(sqliteSettings.DataSource!);
            });
            services.AddFluentMigratorCore()
                .ConfigureRunner(rb =>
                {
                    rb.AddSQLite()
                        .WithGlobalConnectionString(sqliteSettings.DataSource)
                        .ScanIn(typeof(M001_InitialCreate).Assembly).For.All();
                });
        }
        else if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var sqlServerSettings = services.AddSqlServerSettings(configuration, "SqlServer");
            services.AddDbContext<AzureOpsCrewContext>(options =>
            {
                options.UseSqlServer(sqlServerSettings.ConnectionString!);
            });
            services.AddFluentMigratorCore()
                .ConfigureRunner(rb =>
                {
                    rb.AddSqlServer()
                        .WithGlobalConnectionString(sqlServerSettings.ConnectionString)
                        .ScanIn(typeof(M001_InitialCreate).Assembly).For.All();
                });
        }
        else
        {
            throw new InvalidOperationException($"Unknown DB provider '{provider}'. Supported providers: Sqlite, SqlServer");
        }
    }

    public static void AddProviderFacades(this IServiceCollection services)
    {
        services.AddTransient<IProviderFacadeResolver, ProviderFacadeResolver>();
        services.AddHttpClient<AnthropicProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<AzureFoundryProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OllamaProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OpenAIProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddHttpClient<OpenRouterProviderFacade>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
    }

    public static void AddAgentFactory(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAiAgentFactory, AiAgentFactory>();

        var memoryType = configuration["LongTermMemory:Type"] ?? "InMemory";

        services.AddSingleton(p => new AgentAIContextProviderFactory(p, memoryType));
        services.AddSingleton<InMemoryFactsStore>();

        if (string.Equals(memoryType, "Cypher", StringComparison.OrdinalIgnoreCase))
        {
            var uri = configuration["LongTermMemory:Neo4j:Uri"] ?? "bolt://localhost:7687";
            var username = configuration["LongTermMemory:Neo4j:Username"] ?? "neo4j";
            var password = configuration["LongTermMemory:Neo4j:Password"] ?? "password";

            services.AddSingleton<Neo4j.Driver.IDriver>(_ =>
                Neo4j.Driver.GraphDatabase.Driver(uri, Neo4j.Driver.AuthTokens.Basic(username, password)));

            services.AddSingleton<CypherFactsStore>();
        }
    }
}
