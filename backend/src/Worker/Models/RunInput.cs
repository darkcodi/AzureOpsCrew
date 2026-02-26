namespace Worker.Models;

public record RunInput(
    Guid AgentId,
    Guid ThreadId,
    Guid RunId,
    TriggerEvent Trigger);
