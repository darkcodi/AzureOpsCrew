using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using Microsoft.EntityFrameworkCore;
using CosmosAgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Cosmos.AgentEntityTypeConfiguration;
using CosmosChannelConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Cosmos.ChannelEntityTypeConfiguration;
using SqliteAgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.AgentEntityTypeConfiguration;
using SqliteChannelConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ChannelEntityTypeConfiguration;

namespace AzureOpsCrew.Infrastructure.Db;

public class AzureOpsCrewContext : DbContext
{
    public AzureOpsCrewContext(DbContextOptions<AzureOpsCrewContext> options)
        : base(options)
    {
    }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Channel> Channels => Set<Channel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        if (provider == "Microsoft.EntityFrameworkCore.Cosmos")
        {
            modelBuilder.HasDefaultContainer("AppContainer");
            modelBuilder.ApplyConfiguration(new CosmosAgentConfig());
            modelBuilder.ApplyConfiguration(new CosmosChannelConfig());
        }
        else
        {
            // Relational providers (SQLite, SQL Server, etc.)
            modelBuilder.ApplyConfiguration(new SqliteAgentConfig());
            modelBuilder.ApplyConfiguration(new SqliteChannelConfig());
        }
    }
}
