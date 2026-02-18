using AzureOpsCrew.Domain.Providers;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public static class ProviderMappings
{
    public static ProviderResponseDto ToResponseDto(this Provider provider)
    {
        return new ProviderResponseDto
        {
            Id = provider.Id,
            Name = provider.Name,
            ProviderType = provider.ProviderType.ToString(),
            ApiEndpoint = provider.ApiEndpoint,
            DefaultModel = provider.DefaultModel,
            IsEnabled = provider.IsEnabled,
            SelectedModels = provider.SelectedModels,
            ModelsCount = provider.ModelsCount,
            DateCreated = provider.DateCreated,
            DateModified = provider.DateModified,
            HasApiKey = !string.IsNullOrEmpty(provider.ApiKey)
        };
    }

    public static ProviderResponseDto[] ToResponseDtoArray(this IEnumerable<Provider> providers)
    {
        return providers.Select(ToResponseDto).ToArray();
    }
}
