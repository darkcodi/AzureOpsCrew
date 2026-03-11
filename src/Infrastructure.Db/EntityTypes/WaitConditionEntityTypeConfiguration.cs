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

        // Common properties
        builder.Property(w => w.Type)
               .IsRequired();

        builder.Property(w => w.AgentId)
               .IsRequired();

        builder.Property(w => w.ChatId)
               .IsRequired();

        builder.Property(w => w.CreatedAt)
               .IsRequired();

        builder.Property(w => w.CompletedAt);

        builder.Property(w => w.SatisfiedByTriggerId);

        // Message wait condition specific property
        builder.Property(w => w.MessageAfterDateTime);

        // Tool approval wait condition specific property
        builder.Property(w => w.ToolCallId);

        // Indexes
        builder.HasIndex(w => w.AgentId);
        builder.HasIndex(w => w.ChatId);
        builder.HasIndex(w => w.Type);
        builder.HasIndex(w => w.SatisfiedByTriggerId);
    }
}
