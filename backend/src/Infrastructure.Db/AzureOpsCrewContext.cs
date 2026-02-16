using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Providers;
using Microsoft.EntityFrameworkCore;
using AgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.AgentEntityTypeConfiguration;
using ChannelConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ChannelEntityTypeConfiguration;
using ProviderConfigConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ProviderConfigEntityTypeConfiguration;

namespace AzureOpsCrew.Infrastructure.Db;

public class AzureOpsCrewContext : DbContext
{
    public AzureOpsCrewContext(DbContextOptions<AzureOpsCrewContext> options)
        : base(options)
    {
    }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<ProviderConfig> ProviderConfigs => Set<ProviderConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AgentConfig());
        modelBuilder.ApplyConfiguration(new ChannelConfig());
        modelBuilder.ApplyConfiguration(new ProviderConfigConfig());
    }
}
