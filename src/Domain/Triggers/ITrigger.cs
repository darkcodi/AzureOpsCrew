namespace AzureOpsCrew.Domain.Triggers;

public interface ITrigger
{
    Guid Id { get; }
    TriggerType Type { get; }
    Guid AgentId { get; }
    Guid ChatId { get; }
    DateTime CreatedAt { get; }
    DateTime? StartedAt { get; set; }
    DateTime? CompletedAt { get; set; }
    bool IsSkipped { get; set; }
}
