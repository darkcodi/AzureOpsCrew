using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class AocChatConfig : IEntityTypeConfiguration<AocChat>
{
    public void Configure(EntityTypeBuilder<AocChat> builder)
    {
        builder.ToTable("Chats");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
               .ValueGeneratedOnAdd();

        builder.Property(c => c.CreatedAt)
               .IsRequired();

        // No FK constraint - relationship is query-only, not enforced by database
        builder.HasMany(c => c.Messages)
               .WithOne(m => m.Chat)
               .HasPrincipalKey(c => c.Id);
    }
}
