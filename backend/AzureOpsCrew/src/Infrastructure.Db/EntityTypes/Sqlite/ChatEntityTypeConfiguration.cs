using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class ChatEntityTypeConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToTable(nameof(Chat));

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
               .ValueGeneratedOnAdd();

        builder.Property(c => c.ClientId)
               .IsRequired();

        builder.HasIndex(c => c.ClientId);

        builder.Property(c => c.Name)
               .IsRequired();

        builder.Property(c => c.Description);

        builder.Property(c => c.ConversationId);

        builder.Property(c => c.AgentIds)
               .HasConversion(
                   v => v == null ? null : string.Join(',', v),
                   s => string.IsNullOrEmpty(s) ? System.Array.Empty<string>() : s.Split(',', StringSplitOptions.RemoveEmptyEntries));

        builder.Property(c => c.DateCreated)
               .IsRequired();
    }
}
