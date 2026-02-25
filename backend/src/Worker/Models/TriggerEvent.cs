namespace Worker.Models;

public record TriggerEvent(
    Guid TriggerId,
    TriggerSource Source,
    DateTime CreatedAt,
    string? Text = null);
