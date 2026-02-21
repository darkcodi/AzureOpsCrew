using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Users;
using Microsoft.EntityFrameworkCore;
using AgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.AgentEntityTypeConfiguration;
using ChannelConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ChannelEntityTypeConfiguration;
using AiProviderConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ProviderEntityTypeConfiguration;
using UserConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.UserEntityTypeConfiguration;
using AiProvider = AzureOpsCrew.Domain.Providers.Provider;

namespace AzureOpsCrew.Infrastructure.Db;

public class AzureOpsCrewContext : DbContext
{
    public AzureOpsCrewContext(DbContextOptions<AzureOpsCrewContext> options)
        : base(options)
    {
    }

    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<AiProvider> Providers => Set<AiProvider>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AgentConfig());
        modelBuilder.ApplyConfiguration(new ChannelConfig());
        modelBuilder.ApplyConfiguration(new AiProviderConfig());
        modelBuilder.ApplyConfiguration(new UserConfig());
    }
}
