using AzureOpsCrew.Domain.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class TriggerEntityTypeConfiguration : IEntityTypeConfiguration<Trigger>
{
    public void Configure(EntityTypeBuilder<Trigger> builder)
    {
        builder.ToTable("Triggers");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
               .ValueGeneratedOnAdd();

        // Configure TPH discriminator
        builder.HasDiscriminator<int>("Type")
               .HasValue<MessageTrigger>(0)
               .HasValue<ToolApprovalTrigger>(1);

        // Common properties
        builder.Property(t => t.AgentId)
               .IsRequired();

        builder.Property(t => t.ChatId)
               .IsRequired();

        builder.Property(t => t.CreatedAt)
               .IsRequired();

        builder.Property(t => t.StartedAt);

        builder.Property(t => t.CompletedAt);

        builder.Property(t => t.IsSkipped)
               .IsRequired()
               .HasDefaultValue(false);

        // Indexes
        builder.HasIndex(t => t.AgentId);
        builder.HasIndex(t => t.ChatId);
        builder.HasIndex("Type");
    }
}
