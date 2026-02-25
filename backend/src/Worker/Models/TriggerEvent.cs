namespace Worker.Models;

public record TriggerEvent(
    Guid TriggerId,
    TriggerSource Source,
    string? Text = null);
