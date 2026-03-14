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
               .ValueGeneratedOnAdd()
               .HasColumnType("uuid");

        // Configure TPH discriminator
        builder.HasDiscriminator<int>("Type")
               .HasValue<MessageWaitCondition>(0)
               .HasValue<ToolApprovalWaitCondition>(1);

        // Common properties
        builder.Property(w => w.AgentId)
               .IsRequired()
               .HasColumnType("uuid");

        builder.Property(w => w.ChatId)
               .IsRequired()
               .HasColumnType("uuid");

        builder.Property(w => w.CreatedAt)
               .IsRequired();

        builder.Property(w => w.CompletedAt);

        builder.Property(w => w.SatisfiedByTriggerId)
               .HasColumnType("uuid");

        // Indexes
        builder.HasIndex(w => w.AgentId);
        builder.HasIndex(w => w.ChatId);
        builder.HasIndex("Type");
        builder.HasIndex(w => w.SatisfiedByTriggerId);
    }
}
