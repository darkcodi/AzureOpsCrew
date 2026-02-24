namespace Worker.Models;

public record AgentStatusDto(
    AgentStatus Status,
    string? CurrentRunId,
    PendingQuestion? PendingQuestion,
    int QueueDepth,
    int RunNumber);
