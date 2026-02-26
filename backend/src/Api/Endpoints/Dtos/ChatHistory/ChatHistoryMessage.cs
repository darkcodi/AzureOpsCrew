namespace AzureOpsCrew.Api.Endpoints.Dtos.ChatHistory;

public record ChatHistoryMessage
{
    public required string Id { get; init; }
    public required string Role { get; init; }  // "user" or "assistant"
    public required string Content { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}

public record ChatHistoryResponse
{
    public required List<ChatHistoryMessage> Messages { get; init; }
}
