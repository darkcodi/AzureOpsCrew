using AzureOpsCrew.Domain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Sqlite;

public sealed class ProviderConfigEntityTypeConfiguration : IEntityTypeConfiguration<ProviderConfig>
{
    public void Configure(EntityTypeBuilder<ProviderConfig> builder)
    {
        builder.ToTable(nameof(ProviderConfig));

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
               .ValueGeneratedOnAdd();
        builder.Property(x => x.Id)
               .HasConversion(
                   v => v.ToString("D"),
                   s => Guid.Parse(s));

        builder.Property(p => p.ClientId)
               .IsRequired();

        builder.HasIndex(p => p.ClientId);

        builder.Property(p => p.Name)
               .IsRequired()
               .HasMaxLength(200);

        builder.Property(p => p.ProviderType)
               .HasConversion(
                   p => p.ToString(),
                   s => Enum.Parse<ProviderType>(s));

        builder.Property(p => p.ApiKey)
               .IsRequired()
               .HasMaxLength(500);

        builder.Property(p => p.ApiEndpoint)
               .HasMaxLength(500);

        builder.Property(p => p.DefaultModel)
               .HasMaxLength(200);

        builder.Property(p => p.IsEnabled)
               .HasDefaultValue(true);

        builder.Property(p => p.DateCreated)
               .IsRequired();

        builder.Property(p => p.DateModified);
    }
}
