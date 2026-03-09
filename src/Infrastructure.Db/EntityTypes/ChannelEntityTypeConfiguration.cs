using AzureOpsCrew.Domain.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class ChannelEntityTypeConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("Channels");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
               .ValueGeneratedOnAdd();

        builder.Property(c => c.Name)
               .IsRequired();

        builder.Property(c => c.Description);

        builder.Property(c => c.ConversationId);

        builder.Property(c => c.AgentIds)
               .HasConversion(
                   v => v == null ? null : string.Join(',', v),
                   s => string.IsNullOrEmpty(s) ? System.Array.Empty<Guid>() : s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToArray());

        builder.Property(c => c.ManagerAgentId);

        builder.Property(c => c.DateCreated)
               .IsRequired();
    }
}
