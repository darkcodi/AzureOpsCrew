namespace AzureOpsCrew.Infrastructure.Ai.Models;

public record TriggerEvent(
    Guid TriggerId,
    TriggerSource Source,
    DateTime CreatedAt,
    Guid ThreadId,
    Guid RunId,
    string? Text = null);
