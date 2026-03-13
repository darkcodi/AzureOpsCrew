using AzureOpsCrew.Domain.WaitConditions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class WaitConditionEntityTypeConfiguration : IEntityTypeConfiguration<WaitCondition>
{
    public void Configure(EntityTypeBuilder<WaitCondition> builder)
    {
        builder.ToTable("WaitConditions");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
               .ValueGeneratedOnAdd();

        // Configure TPH discriminator
        builder.HasDiscriminator<int>("Type")
               .HasValue<MessageWaitCondition>(0)
               .HasValue<ToolApprovalWaitCondition>(1);

        // Common properties
        builder.Property(w => w.AgentId)
               .IsRequired();

        builder.Property(w => w.ChatId)
               .IsRequired();

        builder.Property(w => w.CreatedAt)
               .IsRequired();

        builder.Property(w => w.CompletedAt);

        builder.Property(w => w.SatisfiedByTriggerId);

        // Indexes
        builder.HasIndex(w => w.AgentId);
        builder.HasIndex(w => w.ChatId);
        builder.HasIndex("Type");
        builder.HasIndex(w => w.SatisfiedByTriggerId);
    }
}
