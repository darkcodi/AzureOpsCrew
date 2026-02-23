using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class MessageEntityTypeConfiguration : IEntityTypeConfiguration<AocMessage>
{
    public void Configure(EntityTypeBuilder<AocMessage> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.ChatId)
               .IsRequired();

        builder.Property(m => m.AuthorName)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(m => m.Text)
               .IsRequired();

        builder.Property(m => m.PostedAt)
               .IsRequired();

        builder.HasIndex(m => m.PostedAt);
        builder.HasIndex(m => m.ChatId);
    }
}
