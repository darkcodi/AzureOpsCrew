namespace Front.Models;

public record AgentMindResponseDto
{
    public required List<AgentMindEventDto> Events { get; init; }
}

public record AgentMindEventDto
{
    public required string Id { get; init; }
    public required string Role { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? Content { get; init; }
    public UiWidgetDto? Widget { get; init; }
    public string? Reasoning { get; init; }
}

public record UiWidgetDto
{
    public required string ToolName { get; init; }
    public required string CallId { get; init; }
    public required object? Args { get; init; }
    public required object? Result { get; init; }
}
