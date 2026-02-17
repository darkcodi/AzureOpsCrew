using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using Microsoft.EntityFrameworkCore;
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
        modelBuilder.ApplyConfiguration(new SqliteAgentConfig());
        modelBuilder.ApplyConfiguration(new SqliteChannelConfig());
    }
}
