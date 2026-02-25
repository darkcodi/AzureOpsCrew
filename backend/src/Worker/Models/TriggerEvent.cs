namespace Worker.Models;

public record TriggerEvent(
    Guid TriggerId,
    TriggerSource Source,
    Guid AgentId,
    string? Text = null);
