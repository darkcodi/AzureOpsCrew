using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class AocMessageConfig : IEntityTypeConfiguration<AocMessage>
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

        // Sender: exactly one of AgentId or UserId should be set
        builder.Property(m => m.AgentId)
               .IsRequired(false);

        builder.Property(m => m.UserId)
               .IsRequired(false);

        // Destination: exactly one of ChannelId or DmId should be set
        builder.Property(m => m.ChannelId)
               .IsRequired(false);

        builder.Property(m => m.DmId)
               .IsRequired(false);

        builder.HasIndex(m => m.PostedAt);
        builder.HasIndex(m => m.ChatId);
        builder.HasIndex(m => m.AgentId);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.ChannelId);
        builder.HasIndex(m => m.DmId);
    }
}
