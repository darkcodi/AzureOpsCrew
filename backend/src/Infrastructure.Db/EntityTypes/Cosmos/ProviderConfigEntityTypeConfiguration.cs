using AzureOpsCrew.Domain.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AzureOpsCrew.Infrastructure.Db.EntityTypes.Cosmos;

public sealed class ProviderConfigEntityTypeConfiguration : IEntityTypeConfiguration<ProviderConfig>
{
    public void Configure(EntityTypeBuilder<ProviderConfig> builder)
    {
        builder.ToContainer("ProviderConfig");

        builder.HasPartitionKey(p => p.ClientId);

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
               .ToJsonProperty("id")
               .HasConversion(
                   g => g.ToString("D"),
                   s => Guid.Parse(s));

        builder.Property(p => p.Name).ToJsonProperty("name");
        builder.Property(p => p.ProviderType).ToJsonProperty("providerType");
        builder.Property(p => p.ApiKey).ToJsonProperty("apiKey");
        builder.Property(p => p.ApiEndpoint).ToJsonProperty("apiEndpoint");
        builder.Property(p => p.DefaultModel).ToJsonProperty("defaultModel");
        builder.Property(p => p.SelectedModels).ToJsonProperty("selectedModels");
        builder.Property(p => p.IsEnabled).ToJsonProperty("isEnabled");
        builder.Property(p => p.DateCreated).ToJsonProperty("dateCreated");
        builder.Property(p => p.DateModified).ToJsonProperty("dateModified");
    }
}
