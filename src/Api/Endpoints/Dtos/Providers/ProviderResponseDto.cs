namespace AzureOpsCrew.Api.Endpoints.Dtos.Providers;

public record ProviderResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ProviderType { get; init; } = string.Empty;
    public string? ApiEndpoint { get; init; }
    public string? DefaultModel { get; init; }
    public bool IsEnabled { get; init; }
    public string? SelectedModels { get; init; }
    public int ModelsCount { get; init; }
    public DateTime DateCreated { get; init; }
    public DateTime? DateModified { get; init; }
    public bool HasApiKey { get; init; }
}
