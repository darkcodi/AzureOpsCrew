using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Domain.WaitConditions;

public class MessageWaitCondition : IWaitCondition
{
    // common
    public Guid Id { get; set; }
    public WaitConditionType Type => WaitConditionType.Message;
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? SatisfiedByTriggerId { get; set; }

    // specific to wait message trigger
    public DateTime MessageAfterDateTime { get; set; }

    public bool CanBeSatisfiedByTrigger(ITrigger trigger)
    {
        if (trigger.Type != TriggerType.Message)
            return false;

        var messageTrigger = (MessageTrigger)trigger;
        return messageTrigger.ChatId == ChatId && messageTrigger.CreatedAt >= MessageAfterDateTime;
    }
}
