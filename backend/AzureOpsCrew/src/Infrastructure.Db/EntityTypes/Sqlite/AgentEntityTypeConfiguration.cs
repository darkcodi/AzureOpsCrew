using AzureOpsCrew.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class AgentEntityTypeConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable(nameof(Agent));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
               .ValueGeneratedOnAdd();

        builder.Property(a => a.ProviderAgentId)
               .IsRequired();

        builder.Property(a => a.ClientId)
               .IsRequired();

        builder.HasIndex(a => a.ClientId);

        builder.OwnsOne(a => a.Info, infoBuilder =>
        {
            infoBuilder.Property(i => i.Name)
                       .IsRequired();

            infoBuilder.Property(i => i.Prompt)
                       .IsRequired();

            infoBuilder.Property(i => i.Model)
                       .IsRequired();

            infoBuilder.Property(i => i.Description);

            infoBuilder.Property(i => i.AvaliableTools)
                       .HasConversion(
                           v => v == null ? null : string.Join(',', v.Select(x => x.ToString())),
                           s => string.IsNullOrEmpty(s) ? System.Array.Empty<AgentTool>() : s.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => Enum.Parse<AgentTool>(x)).ToArray());
        });

        builder.Property(a => a.Provider)
               .HasConversion(
                   p => p.ToString(),
                   s => Enum.Parse<Provider>(s));

        builder.Property(a => a.DateCreated)
               .IsRequired();
    }
}
