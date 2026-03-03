using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Execution;
using AzureOpsCrew.Domain.Users;
using Microsoft.EntityFrameworkCore;
using AgentConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.AgentEntityTypeConfiguration;
using ChannelConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ChannelEntityTypeConfiguration;
using AiProviderConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ProviderEntityTypeConfiguration;
using PendingRegistrationConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.PendingRegistrationEntityTypeConfiguration;
using UserConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.UserEntityTypeConfiguration;
using ExecutionRunConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ExecutionRunEntityTypeConfiguration;
using ExecutionTaskConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ExecutionTaskEntityTypeConfiguration;
using ArtifactConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ArtifactEntityTypeConfiguration;
using JournalEntryConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.JournalEntryEntityTypeConfiguration;
using ApprovalRequestConfig = AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite.ApprovalRequestEntityTypeConfiguration;
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
    public DbSet<PendingRegistration> PendingRegistrations => Set<PendingRegistration>();

    // Execution Engine
    public DbSet<ExecutionRun> ExecutionRuns => Set<ExecutionRun>();
    public DbSet<ExecutionTask> ExecutionTasks => Set<ExecutionTask>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AgentConfig());
        modelBuilder.ApplyConfiguration(new ChannelConfig());
        modelBuilder.ApplyConfiguration(new AiProviderConfig());
        modelBuilder.ApplyConfiguration(new UserConfig());
        modelBuilder.ApplyConfiguration(new PendingRegistrationConfig());

        // Execution Engine
        modelBuilder.ApplyConfiguration(new ExecutionRunConfig());
        modelBuilder.ApplyConfiguration(new ExecutionTaskConfig());
        modelBuilder.ApplyConfiguration(new ArtifactConfig());
        modelBuilder.ApplyConfiguration(new JournalEntryConfig());
        modelBuilder.ApplyConfiguration(new ApprovalRequestConfig());
    }
}
