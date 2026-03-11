#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Triggers;

public class AgentTriggerExecution
{
    private AgentTriggerExecution() { }

    public AgentTriggerExecution(Guid triggerId, string? contextJson = null)
    {
        Id = Guid.NewGuid();
        TriggerId = triggerId;
        FiredAt = DateTime.UtcNow;
        ContextJson = contextJson;
        Success = true;
    }

    public Guid Id { get; private set; }
    public Guid TriggerId { get; private set; }
    public DateTime FiredAt { get; private set; }
    public string? ContextJson { get; private set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    public void MarkFailed(string error)
    {
        Success = false;
        ErrorMessage = error;
        CompletedAt = DateTime.UtcNow;
    }

    public void MarkCompleted()
    {
        CompletedAt = DateTime.UtcNow;
    }
}
