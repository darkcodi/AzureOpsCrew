using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Domain.WaitConditions;

public class ToolApprovalWaitCondition : IWaitCondition
{
    // common
    public Guid Id { get; set; }
    public WaitConditionType Type => WaitConditionType.ToolApproval;
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? SatisfiedByTriggerId { get; set; }

    // specific to wait tool approval trigger
    public string ToolCallId { get; set; } = string.Empty;

    public bool CanBeSatisfiedByTrigger(ITrigger trigger)
    {
        if (trigger.Type != TriggerType.ToolApproval)
            return false;

        var toolApprovalTrigger = (ToolApprovalTrigger)trigger;
        return toolApprovalTrigger.ChatId == ChatId && string.Equals(toolApprovalTrigger.CallId, ToolCallId, StringComparison.InvariantCultureIgnoreCase);
    }
}
