using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class ChatEntityTypeConfiguration : IEntityTypeConfiguration<Chat>
{
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.ToTable("Chats");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
               .ValueGeneratedOnAdd();

        builder.Property(c => c.Title)
               .IsRequired()
               .HasMaxLength(256);

        builder.Property(c => c.ParticipantUserIds)
               .HasConversion(
                   v => v == null ? null : string.Join(',', v),
                   s => string.IsNullOrEmpty(s) ? Array.Empty<int>() : s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray());

        builder.Property(c => c.ParticipantAgentIds)
               .HasConversion(
                   v => v == null ? null : string.Join(',', v),
                   s => string.IsNullOrEmpty(s) ? Array.Empty<Guid>() : s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray());

        builder.Property(c => c.CreatedAt)
               .IsRequired();

        builder.Property(c => c.DateModified);
    }
}
