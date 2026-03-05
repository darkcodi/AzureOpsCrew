using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.McpServerConfigurations;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using AgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.AgentEntityTypeConfiguration;
using ChannelConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.ChannelEntityTypeConfiguration;
using McpServerConfigurationConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.McpServerConfigurationEntityTypeConfiguration;
using AiProviderConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.ProviderEntityTypeConfiguration;
using PendingRegistrationConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.PendingRegistrationEntityTypeConfiguration;
using UserConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.UserEntityTypeConfiguration;
using MessageConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.MessageEntityTypeConfiguration;
using DirectMessageChannelConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.DirectMessageChannelEntityTypeConfiguration;
using AgentThoughtConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.AgentThoughtEntityTypeConfiguration;
using RawLlmHttpCallConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.RawLlmHttpCallEntityTypeConfiguration;
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
    public DbSet<McpServerConfiguration> McpServerConfigurations => Set<McpServerConfiguration>();
    public DbSet<AiProvider> Providers => Set<AiProvider>();
    public DbSet<User> Users => Set<User>();
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<DirectMessageChannel> Dms => Set<DirectMessageChannel>();
    public DbSet<AgentThought> AgentThoughts => Set<AgentThought>();
    public DbSet<RawLlmHttpCall> RawLlmHttpCalls => Set<RawLlmHttpCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AgentConfig());
        modelBuilder.ApplyConfiguration(new ChannelConfig());
        modelBuilder.ApplyConfiguration(new McpServerConfigurationConfig());
        modelBuilder.ApplyConfiguration(new AiProviderConfig());
        modelBuilder.ApplyConfiguration(new UserConfig());
        modelBuilder.ApplyConfiguration(new PendingRegistrationConfig());
        modelBuilder.ApplyConfiguration(new MessageConfig());
        modelBuilder.ApplyConfiguration(new DirectMessageChannelConfig());
        modelBuilder.ApplyConfiguration(new AgentThoughtConfig());
        modelBuilder.ApplyConfiguration(new RawLlmHttpCallConfig());
    }
}



