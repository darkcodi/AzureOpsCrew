using AzureOpsCrew.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class AgentSnapshotConfig : IEntityTypeConfiguration<AgentSnapshot>
{
    public void Configure(EntityTypeBuilder<AgentSnapshot> builder)
    {
        builder.ToTable("AgentSnapshots");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
               .ValueGeneratedOnAdd();

        builder.Property(s => s.AgentId)
               .IsRequired();

        builder.HasIndex(s => s.AgentId)
               .IsUnique();

        builder.Property(s => s.MemorySummary)
               .IsRequired();

        builder.OwnsMany(s => s.RecentTranscript, transcriptBuilder =>
        {
            transcriptBuilder.Property(t => t.Role)
                           .IsRequired();

            transcriptBuilder.Property(t => t.Text)
                           .IsRequired();
        });

        builder.Property(s => s.CreatedAt)
               .IsRequired();

        builder.Property(s => s.UpdatedAt)
               .IsRequired();
    }
}
