using AzureOpsCrew.Domain.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class AgentTriggerExecutionEntityTypeConfiguration : IEntityTypeConfiguration<AgentTriggerExecution>
{
    public void Configure(EntityTypeBuilder<AgentTriggerExecution> builder)
    {
        builder.ToTable("TriggerExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .ValueGeneratedOnAdd();

        builder.Property(e => e.TriggerId)
            .IsRequired();

        builder.Property(e => e.FiredAt)
            .IsRequired();

        builder.Property(e => e.ContextJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(e => e.Success)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasColumnType("nvarchar(max)")
            .IsRequired(false);

        builder.Property(e => e.CompletedAt)
            .IsRequired(false);

        // Index for querying executions by trigger
        builder.HasIndex(e => e.TriggerId)
            .HasDatabaseName("IX_TriggerExecutions_TriggerId");

        // Index for querying recent executions
        builder.HasIndex(e => e.FiredAt)
            .HasDatabaseName("IX_TriggerExecutions_FiredAt");
    }
}
