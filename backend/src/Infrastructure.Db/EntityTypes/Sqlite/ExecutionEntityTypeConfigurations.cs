using AzureOpsCrew.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public class ExecutionRunEntityTypeConfiguration : IEntityTypeConfiguration<ExecutionRun>
{
    public void Configure(EntityTypeBuilder<ExecutionRun> builder)
    {
        builder.ToTable("ExecutionRun");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion<string>();
        builder.Property(e => e.ChannelId).HasConversion<string>();
        builder.Property(e => e.Status).HasConversion<int>();

        builder.HasMany(e => e.Tasks).WithOne(t => t.Run).HasForeignKey(t => t.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Artifacts).WithOne(a => a.Run).HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Journal).WithOne(j => j.Run).HasForeignKey(j => j.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.ApprovalRequests).WithOne(a => a.Run).HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExecutionTaskEntityTypeConfiguration : IEntityTypeConfiguration<ExecutionTask>
{
    public void Configure(EntityTypeBuilder<ExecutionTask> builder)
    {
        builder.ToTable("ExecutionTask");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion<string>();
        builder.Property(e => e.RunId).HasConversion<string>();
        builder.Property(e => e.ParentTaskId).HasConversion<string>();
        builder.Property(e => e.Status).HasConversion<int>();
        builder.Property(e => e.TaskType).HasConversion<int>();

        builder.HasOne(e => e.ParentTask).WithMany(e => e.ChildTasks).HasForeignKey(e => e.ParentTaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(e => e.Artifacts).WithOne(a => a.Task).HasForeignKey(a => a.TaskId).OnDelete(DeleteBehavior.SetNull);
    }
}

public class ArtifactEntityTypeConfiguration : IEntityTypeConfiguration<Artifact>
{
    public void Configure(EntityTypeBuilder<Artifact> builder)
    {
        builder.ToTable("Artifact");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion<string>();
        builder.Property(e => e.RunId).HasConversion<string>();
        builder.Property(e => e.TaskId).HasConversion<string>();
        builder.Property(e => e.ArtifactType).HasConversion<int>();
    }
}

public class JournalEntryEntityTypeConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("JournalEntry");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion<string>();
        builder.Property(e => e.RunId).HasConversion<string>();
        builder.Property(e => e.TaskId).HasConversion<string>();
        builder.Property(e => e.EntryType).HasConversion<int>();
    }
}

public class ApprovalRequestEntityTypeConfiguration : IEntityTypeConfiguration<ApprovalRequest>
{
    public void Configure(EntityTypeBuilder<ApprovalRequest> builder)
    {
        builder.ToTable("ApprovalRequest");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasConversion<string>();
        builder.Property(e => e.RunId).HasConversion<string>();
        builder.Property(e => e.TaskId).HasConversion<string>();
        builder.Property(e => e.RiskLevel).HasConversion<int>();
        builder.Property(e => e.Status).HasConversion<int>();
    }
}
