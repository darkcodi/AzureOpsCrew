namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record McpServerConfigurationAuthResponseDto
{
    public string Type { get; init; } = string.Empty;
    public bool HasBearerToken { get; init; }
    public bool HasApiKey { get; init; }
    public string? ApiKeyHeaderName { get; init; }
}