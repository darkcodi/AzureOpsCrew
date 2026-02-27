namespace Worker.Models;

public record AgentStatusDto(
    string AgentStatus,
    Guid? RunId,
    int QueueDepth,
    long RunNumber,
    string? Error);
