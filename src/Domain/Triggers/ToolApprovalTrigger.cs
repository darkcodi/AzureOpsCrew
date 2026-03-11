using AzureOpsCrew.Domain.Tools;

namespace AzureOpsCrew.Domain.Triggers;

public class ToolApprovalTrigger : ITrigger
{
    // common
    public Guid Id { get; set; }
    public TriggerType Type => TriggerType.ToolApproval;
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsSkipped { get; set; }

    // specific
    public string CallId { get; set; } = string.Empty;
    public ApprovalResolution Resolution { get; set; } = ApprovalResolution.None;
    public string ToolName { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
}
