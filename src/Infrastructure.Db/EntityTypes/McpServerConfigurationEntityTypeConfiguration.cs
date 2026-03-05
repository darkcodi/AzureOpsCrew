using AzureOpsCrew.Domain.McpServerConfigurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes;

public sealed class McpServerConfigurationEntityTypeConfiguration : IEntityTypeConfiguration<McpServerConfiguration>
{
    public void Configure(EntityTypeBuilder<McpServerConfiguration> builder)
    {
        builder.ToTable("McpServerConfigurations");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(4000);

        builder.Property(x => x.Url)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.IsEnabled)
            .HasDefaultValue(true);

        builder.Property(x => x.ToolsSyncedAt);

        builder.Property(x => x.DateCreated)
            .IsRequired();

        builder.OwnsMany(x => x.Tools, toolsBuilder =>
        {
            toolsBuilder.ToTable("McpServerConfigurationTools");
            toolsBuilder.WithOwner()
                .HasForeignKey("McpServerConfigurationId");

            toolsBuilder.Property<int>("Id")
                .ValueGeneratedOnAdd();
            toolsBuilder.HasKey("Id");

            toolsBuilder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            toolsBuilder.Property(x => x.Description)
                .HasMaxLength(4000);

            toolsBuilder.Property(x => x.InputSchemaJson)
                .HasColumnType("nvarchar(max)");

            toolsBuilder.Property(x => x.OutputSchemaJson)
                .HasColumnType("nvarchar(max)");

            toolsBuilder.Property(x => x.IsEnabled)
                .HasDefaultValue(true);
        });
    }
}
