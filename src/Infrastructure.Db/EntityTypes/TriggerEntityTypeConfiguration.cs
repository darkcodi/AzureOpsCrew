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

        // Common properties
        builder.Property(t => t.Type)
               .HasConversion<string>()
               .IsRequired();

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

        // Message trigger specific properties
        builder.Property(t => t.MessageId);

        builder.Property(t => t.AuthorId);

        builder.Property(t => t.AuthorName)
               .HasMaxLength(200);

        builder.Property(t => t.MessageContent)
               .HasColumnType("nvarchar(max)");

        // Tool approval trigger specific properties
        builder.Property(t => t.CallId);

        builder.Property(t => t.Resolution)
               .HasConversion<string>();

        builder.Property(t => t.ToolName)
               .HasMaxLength(200);

        builder.Property(t => t.Parameters)
               .HasColumnType("nvarchar(max)");

        // Indexes
        builder.HasIndex(t => t.AgentId);
        builder.HasIndex(t => t.ChatId);
        builder.HasIndex(t => t.Type);
    }
}
