using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class LlmChatMessageEntityTypeConfiguration : IEntityTypeConfiguration<LlmChatMessage>
{
    public void Configure(EntityTypeBuilder<LlmChatMessage> builder)
    {
        builder.ToTable("LlmChatMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.AgentId)
               .IsRequired();

        builder.Property(m => m.RunId)
               .IsRequired()
               .HasMaxLength(200);

        builder.HasIndex(m => m.RunId);

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

        builder.HasIndex(m => m.AgentId);
        builder.HasIndex(m => m.CreatedAt);
    }
}
