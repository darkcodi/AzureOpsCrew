using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using CosmosAgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Cosmos.AgentEntityTypeConfiguration;
using CosmosChatConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Cosmos.ChatEntityTypeConfiguration;
using SqliteAgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.AgentEntityTypeConfiguration;
using SqliteChatConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ChatEntityTypeConfiguration;

namespace AzureOpsCrew.Infrastructure.Db;

public class AzureOpsCrewContext : DbContext
{
    public AzureOpsCrewContext(DbContextOptions<AzureOpsCrewContext> options)
        : base(options)
    {
    }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Chat> Chats => Set<Chat>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        if (provider == "Microsoft.EntityFrameworkCore.Cosmos")
        {
            modelBuilder.HasDefaultContainer("AppContainer");
            modelBuilder.ApplyConfiguration(new CosmosAgentConfig());
            modelBuilder.ApplyConfiguration(new CosmosChatConfig());
        }
        else
        {
            // Relational providers (SQLite, SQL Server, etc.)
            modelBuilder.ApplyConfiguration(new SqliteAgentConfig());
            modelBuilder.ApplyConfiguration(new SqliteChatConfig());
        }
    }
}
