using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class AgentThoughtEntityTypeConfiguration : IEntityTypeConfiguration<AgentThought>
{
    public void Configure(EntityTypeBuilder<AgentThought> builder)
    {
        builder.ToTable("AgentThoughts");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.AgentId)
               .IsRequired();

        builder.Property(m => m.RunId)
               .IsRequired();

        builder.Property(m => m.ThreadId)
               .IsRequired();

        builder.HasIndex(m => m.RunId);
        builder.HasIndex(m => m.ThreadId);

        builder.Property(m => m.IsHidden)
               .IsRequired()
               .HasDefaultValue(false);

        builder.Property(m => m.Role)
               .IsRequired()
               .HasConversion(
                   v => v.ToString(),
                   v => new ChatRole(v));

        builder.Property(m => m.AuthorName)
               .IsRequired(false)
               .HasMaxLength(256);

        builder.Property(m => m.CreatedAt)
               .IsRequired();

        builder.Property(m => m.ContentJson)
               .IsRequired();

        builder.Property(m => m.ContentType)
               .IsRequired()
               .HasMaxLength(100)
               .HasConversion(
                   v => v.ToString(),
                   v => Enum.Parse<LlmMessageContentType>(v));

        builder.HasIndex(m => m.AgentId);
        builder.HasIndex(m => m.CreatedAt);

        builder.Property(m => m.ChatMessageId)
               .IsRequired()
               .HasDefaultValue(Guid.Empty);

        builder.HasIndex(m => m.ChatMessageId);
    }
}
