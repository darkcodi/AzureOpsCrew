namespace Worker.Models;

public record RunOutcome(
    RunOutcomeKind Kind,
    FinalAnswer? AgentReply,
    string? Error);
