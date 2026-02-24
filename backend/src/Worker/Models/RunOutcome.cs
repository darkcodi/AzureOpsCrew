namespace Worker.Models;

public record RunOutcome(
    RunOutcomeKind Kind,
    string? AgentReply,
    PendingQuestion? NewPendingQuestion,
    string? MemorySummary);
