namespace Worker.Models;

public record RunInput(
    Guid AgentId,
    TriggerEvent Trigger,
    PendingQuestion? PendingQuestionBefore);
