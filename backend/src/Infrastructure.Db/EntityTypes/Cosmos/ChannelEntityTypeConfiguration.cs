using AzureOpsCrew.Domain.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Cosmos;

public sealed class ChannelEntityTypeConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToContainer(nameof(Channel));

        builder.HasPartitionKey(c => c.ClientId);

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ToJsonProperty("id")
            .HasConversion(
                g => g.ToString("D"),
                s => Guid.Parse(s));
    }
}
