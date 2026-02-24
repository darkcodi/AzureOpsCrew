using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class DmEntityTypeConfiguration : IEntityTypeConfiguration<AocDm>
{
    public void Configure(EntityTypeBuilder<AocDm> builder)
    {
        builder.ToTable("Dms");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
               .ValueGeneratedOnAdd();

        builder.Property(d => d.User1Id)
               .IsRequired(false);

        builder.Property(d => d.User2Id)
               .IsRequired(false);

        builder.Property(d => d.Agent1Id)
               .IsRequired(false);

        builder.Property(d => d.Agent2Id)
               .IsRequired(false);

        builder.Property(d => d.CreatedAt)
               .IsRequired();

        builder.HasIndex(d => d.User1Id);
        builder.HasIndex(d => d.User2Id);
        builder.HasIndex(d => d.Agent1Id);
        builder.HasIndex(d => d.Agent2Id);

        builder.HasMany(d => d.Messages)
               .WithOne()
               .HasForeignKey(m => m.DmId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
