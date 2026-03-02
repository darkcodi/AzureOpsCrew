using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class MessageEntityTypeConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("Messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
               .ValueGeneratedOnAdd();

        builder.Property(m => m.Text)
               .IsRequired();

        builder.Property(m => m.PostedAt)
               .IsRequired();

        // Sender: exactly one of AgentId or UserId should be set
        builder.Property(m => m.AgentId);

        builder.Property(m => m.UserId);

        // Destination: exactly one of ChannelId or DmId should be set
        builder.Property(m => m.ChannelId);

        builder.Property(m => m.DmId);

        builder.HasIndex(m => m.PostedAt);
        builder.HasIndex(m => m.AgentId);
        builder.HasIndex(m => m.UserId);
        builder.HasIndex(m => m.ChannelId);
        builder.HasIndex(m => m.DmId);
    }
}
