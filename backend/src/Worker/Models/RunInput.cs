namespace Worker.Models;

public record RunInput(
    string RunId,
    Guid AgentId,
    TriggerEvent Trigger);
