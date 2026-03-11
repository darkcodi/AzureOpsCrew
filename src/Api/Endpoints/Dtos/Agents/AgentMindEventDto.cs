using System.Text.Json.Serialization;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Agents;

public record AgentMindResponseDto
{
    public required List<AgentMindEventDto> Events { get; init; }
}

public record AgentMindEventDto
{
    public required string Id { get; init; }
    public required string Role { get; init; }  // "user" or "assistant"
    public required DateTimeOffset Timestamp { get; init; }
    public string? Content { get; init; }
    public UiWidgetDto? Widget { get; init; }
    public string? Reasoning { get; init; }
}

public record UiWidgetDto
{
    [JsonPropertyName("toolName")]
    public required string ToolName { get; init; }

    [JsonPropertyName("callId")]
    public required string CallId { get; init; }

    [JsonPropertyName("args")]
    public required object? Args { get; init; }

    [JsonPropertyName("result")]
    public required object? Result { get; init; }

    [JsonPropertyName("isError")]
    public bool IsError { get; init; }
}
