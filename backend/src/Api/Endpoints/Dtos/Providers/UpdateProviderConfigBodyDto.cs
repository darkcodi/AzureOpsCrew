namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public record UpdateProviderConfigBodyDto
{
    public string Name { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string? ApiEndpoint { get; set; }
    public string? DefaultModel { get; set; }
}
