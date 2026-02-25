namespace Worker.Models;

public record RunOutcome(
    RunOutcomeKind Kind,
    string? Error);
