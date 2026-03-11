using AzureOpsCrew.Domain.Triggers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class AgentTriggerEntityTypeConfiguration : IEntityTypeConfiguration<AgentTrigger>
{
    public void Configure(EntityTypeBuilder<AgentTrigger> builder)
    {
        builder.ToTable("Triggers");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.AgentId)
            .IsRequired();

        builder.Property(x => x.ChatId)
            .IsRequired();

        builder.Property(x => x.TriggerType)
            .IsRequired();

        builder.Property(x => x.ConfigurationJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(x => x.IsEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.LastFiredAt);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.HasIndex(x => x.AgentId);
        builder.HasIndex(x => x.ChatId);
        builder.HasIndex(x => new { x.TriggerType, x.IsEnabled });
    }
}
