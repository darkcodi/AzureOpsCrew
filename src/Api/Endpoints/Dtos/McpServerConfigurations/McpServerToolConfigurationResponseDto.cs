namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record McpServerToolConfigurationResponseDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? InputSchemaJson { get; init; }
    public string? OutputSchemaJson { get; init; }
    public bool IsEnabled { get; init; }
}