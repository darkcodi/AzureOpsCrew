namespace AzureOpsCrew.Infrastructure.Ai.Models;

public record RunOutcome(
    RunOutcomeKind Kind,
    string? Error);
