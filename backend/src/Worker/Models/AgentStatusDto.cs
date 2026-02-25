namespace Worker.Models;

public record AgentStatusDto(
    AgentStatus Status,
    string? CurrentRunId,
    int QueueDepth,
    int RunNumber);
