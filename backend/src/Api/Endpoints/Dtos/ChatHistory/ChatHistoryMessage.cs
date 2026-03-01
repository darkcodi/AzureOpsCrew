namespace AzureOpsCrew.Api.Endpoints.Dtos.ChatHistory;

public record ChatHistoryWidget
{
    public required string ToolName { get; init; }
    public required string CallId { get; init; }
    public required object? Args { get; init; }
    public required object? Result { get; init; }
}

public record ChatHistoryMessage
{
    public required string Id { get; init; }
    public required string Role { get; init; }  // "user" or "assistant"
    public required DateTimeOffset Timestamp { get; init; }
    public string? Content { get; init; }
    public ChatHistoryWidget? Widget { get; init; }
    public string? Reasoning { get; init; }
}

public record ChatHistoryResponse
{
    public required List<ChatHistoryMessage> Messages { get; init; }
}
