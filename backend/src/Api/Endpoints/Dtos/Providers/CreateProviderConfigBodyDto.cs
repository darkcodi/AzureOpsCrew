using AzureOpsCrew.Domain.Providers;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public record CreateProviderConfigBodyDto
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ProviderType ProviderType { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
    public string? DefaultModel { get; set; }
}
