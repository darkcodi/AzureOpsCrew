namespace Worker.Models;

public record RunInput(
    Guid RunId,
    Guid AgentId,
    TriggerEvent Trigger);
