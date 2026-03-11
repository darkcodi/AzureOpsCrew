namespace AzureOpsCrew.Domain.Triggers;

public interface ITrigger
{
    Guid Id { get; }
    TriggerType Type { get; }
    Guid AgentId { get; }
    Guid ChatId { get; }
    DateTime CreatedAt { get; }
    DateTime? StartedAt { get; }
    DateTime? CompletedAt { get; }

    Trigger ToDto();
    ITrigger FromDto(Trigger dto);
}
