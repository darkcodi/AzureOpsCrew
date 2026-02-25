namespace Worker.Models;

public record AgentStatusDto(
    AgentStatus Status,
    Guid? RunId,
    int QueueDepth,
    long RunNumber,
    string? Error);
