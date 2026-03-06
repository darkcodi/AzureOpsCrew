namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record McpServerConfigurationResponseDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Url { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public DateTime? ToolsSyncedAt { get; init; }
    public DateTime DateCreated { get; init; }
    public McpServerConfigurationAuthResponseDto Auth { get; init; } = new();
    public McpServerToolConfigurationResponseDto[] Tools { get; init; } = [];
}