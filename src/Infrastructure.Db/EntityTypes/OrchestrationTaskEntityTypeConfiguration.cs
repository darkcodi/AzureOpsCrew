using AzureOpsCrew.Domain.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class OrchestrationTaskEntityTypeConfiguration : IEntityTypeConfiguration<OrchestrationTask>
{
    public void Configure(EntityTypeBuilder<OrchestrationTask> builder)
    {
        builder.ToTable("OrchestrationTasks");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedOnAdd();

        builder.Property(t => t.ChannelId).IsRequired();
        builder.Property(t => t.CreatedByAgentId).IsRequired();
        builder.Property(t => t.AssignedAgentId).IsRequired();

        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);

        builder.Property(t => t.Status).IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.ProgressSummary).HasMaxLength(4000);
        builder.Property(t => t.ResultSummary).HasMaxLength(4000);
        builder.Property(t => t.FailureReason).HasMaxLength(4000);

        builder.Property(t => t.AnnounceInChat).IsRequired();
        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.LastPublicProgressSummary).HasMaxLength(4000);

        builder.HasIndex(t => t.ChannelId);
        builder.HasIndex(t => t.AssignedAgentId);
        builder.HasIndex(t => new { t.ChannelId, t.Status });
    }
}
