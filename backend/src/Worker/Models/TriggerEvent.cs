namespace Worker.Models;

public record TriggerEvent(
    string TriggerId,
    TriggerSource Source,
    Guid AgentId,
    string? Text = null,
    string? AnswerToQuestionId = null);
