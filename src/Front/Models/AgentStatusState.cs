namespace Front.Models;

public record AgentStatusState
{
    public required Guid ConversationId { get; init; }
    public required ConversationType ConversationType { get; init; }
    public required Guid AgentId { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
}

public enum ConversationType
{
    Dm,
    Channel
}
