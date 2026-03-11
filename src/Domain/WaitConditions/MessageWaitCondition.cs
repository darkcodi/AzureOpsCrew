using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Domain.WaitConditions;

public class MessageWaitCondition : WaitCondition
{
    public DateTime MessageAfterDateTime { get; set; }

    public override bool CanBeSatisfiedByTrigger(Trigger trigger)
    {
        if (trigger is not MessageTrigger messageTrigger)
            return false;
        return messageTrigger.ChatId == ChatId && messageTrigger.CreatedAt >= MessageAfterDateTime;
    }
}
