using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Domain.WaitConditions;

public class ToolApprovalWaitCondition : WaitCondition
{
    public string ToolCallId { get; set; } = string.Empty;

    public override bool CanBeSatisfiedByTrigger(Trigger trigger)
    {
        if (trigger is not ToolApprovalTrigger toolApprovalTrigger)
            return false;
        return toolApprovalTrigger.ChatId == ChatId &&
               string.Equals(toolApprovalTrigger.CallId, ToolCallId, StringComparison.InvariantCultureIgnoreCase);
    }
}
