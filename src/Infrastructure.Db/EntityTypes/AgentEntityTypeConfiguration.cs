using System.Text.Json;
using AzureOpsCrew.Domain.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class AgentEntityTypeConfiguration : IEntityTypeConfiguration<Agent>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Agent> builder)
    {
        builder.ToTable("Agents");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
               .ValueGeneratedOnAdd()
               .HasColumnType("uuid");

        builder.Property(a => a.ProviderAgentId)
               .IsRequired();

        builder.OwnsOne(a => a.Info, infoBuilder =>
        {
            infoBuilder.Property(i => i.Username)
                       .IsRequired()
                       .HasMaxLength(30);

            infoBuilder.HasIndex(i => i.Username)
                       .IsUnique();

            infoBuilder.Property(i => i.Prompt)
                       .IsRequired()
                       .HasMaxLength(8000);

            infoBuilder.Property(i => i.Model)
                       .IsRequired();

            infoBuilder.Property(i => i.Description);

            infoBuilder.Property(i => i.AvailableMcpServerTools)
                       .HasConversion(
                           v => JsonSerializer.Serialize(v ?? Array.Empty<AgentMcpServerToolAvailability>(), JsonOptions),
                           s => string.IsNullOrEmpty(s)
                               ? Array.Empty<AgentMcpServerToolAvailability>()
                               : JsonSerializer.Deserialize<AgentMcpServerToolAvailability[]>(s, JsonOptions) ?? Array.Empty<AgentMcpServerToolAvailability>())
                       .HasColumnName("Info_AvailableMcpServerTools");
        });

        builder.Property(a => a.ProviderId)
               .IsRequired()
               .HasColumnType("uuid");

        builder.Property(a => a.Color)
               .IsRequired()
               .HasDefaultValue("#43b581");

        builder.Property(a => a.DateCreated)
               .IsRequired();
    }
}
