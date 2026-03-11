using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Domain.WaitConditions;

public interface IWaitCondition
{
    Guid Id { get; }
    WaitConditionType Type { get; }
    Guid AgentId { get; }
    Guid ChatId { get; }
    DateTime CreatedAt { get; }
    DateTime? CompletedAt { get; set; }
    Guid? SatisfiedByTriggerId { get; set; }

    bool CanBeSatisfiedByTrigger(ITrigger trigger);
}
