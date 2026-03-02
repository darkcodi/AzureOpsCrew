using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class ChatMessageEntityTypeConfiguration : IEntityTypeConfiguration<ChatMessageItem>
{
    public void Configure(EntityTypeBuilder<ChatMessageItem> builder)
    {
        builder.ToTable("ChatMessages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.ChatId)
               .IsRequired();

        builder.Property(m => m.Content)
               .IsRequired()
               .HasMaxLength(30000);

        builder.Property(m => m.SenderId)
               .IsRequired();

        builder.Property(m => m.PostedAt)
               .IsRequired();

        builder.HasIndex(m => m.PostedAt);
        builder.HasIndex(m => m.ChatId);
        builder.HasIndex(m => m.SenderId);
    }
}
