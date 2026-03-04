using AzureOpsCrew.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class AgentEntityTypeConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable(nameof(Agent));

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
               .ValueGeneratedOnAdd();
        builder.Property(x => x.Id)
               .HasConversion(
                   v => v.ToString("D"),
                   s => Guid.Parse(s));

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

            infoBuilder.Property(i => i.AvailableTools)
                       .HasConversion(
                           v => v == null || v.Length == 0 ? "[]" : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                           s => string.IsNullOrEmpty(s) || s == "[]" ? Array.Empty<AgentTool>() : JsonSerializer.Deserialize<AgentTool[]>(s, (JsonSerializerOptions?)null) ?? Array.Empty<AgentTool>());
        });

        builder.Property(a => a.ProviderId)
               .IsRequired();

        builder.Property(a => a.Color)
               .IsRequired()
               .HasDefaultValue("#43b581");

        builder.Property(a => a.DateCreated)
               .IsRequired();
    }
}
