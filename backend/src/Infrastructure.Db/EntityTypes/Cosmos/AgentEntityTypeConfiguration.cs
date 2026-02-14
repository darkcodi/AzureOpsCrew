using AzureOpsCrew.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Cosmos;

public sealed class AgentEntityTypeConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToContainer(nameof(Agent));

        builder.HasPartitionKey(a => a.ClientId);

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
               .ToJsonProperty("id")
               .HasConversion(
                    g => g.ToString("D"),
                    s => Guid.Parse(s));

        builder.OwnsOne(a => a.Info, infoBuilder =>
        {
            infoBuilder.Property(a => a.AvaliableTools)
               .HasConversion(
                   v => v.Select(x => (int)x).ToArray(),
                   v => v.Select(x => (AgentTool)x).ToArray());
        });
    }
}
