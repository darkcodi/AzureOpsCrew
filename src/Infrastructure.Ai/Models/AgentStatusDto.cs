namespace AzureOpsCrew.Infrastructure.Ai.Models;

public record AgentStatusDto(
    string AgentStatus,
    Guid? RunId,
    int QueueDepth,
    long RunNumber,
    string? Error);
