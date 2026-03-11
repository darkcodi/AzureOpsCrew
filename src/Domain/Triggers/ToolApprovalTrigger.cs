using AzureOpsCrew.Domain.Tools;

namespace AzureOpsCrew.Domain.Triggers;

public class ToolApprovalTrigger : Trigger
{
    public string CallId { get; set; } = string.Empty;
    public ApprovalResolution Resolution { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
}
