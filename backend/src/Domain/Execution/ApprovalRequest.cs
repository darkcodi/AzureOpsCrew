namespace AzureOpsCrew.Domain.Execution;

/// <summary>
/// Approval request for risky operations. Engine halts until user decides.
/// Contains the full approval package: action, evidence, risk, rollback plan.
/// </summary>
public class ApprovalRequest
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid? TaskId { get; set; }

    public string ActionType { get; set; } = ""; // deploy, rollback, restart, etc.
    public string ProposedAction { get; set; } = "";
    public string? Target { get; set; } // resource/service being affected
    public string? EvidenceRefs { get; set; } // comma-separated artifact IDs
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Medium;
    public string? RollbackPlan { get; set; }
    public string? VerificationPlan { get; set; }
    public string? AffectedResources { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? DecisionReason { get; set; }

    public DateTime RequestedAt { get; private set; }
    public DateTime? RespondedAt { get; set; }
    public string? RespondedBy { get; set; }

    // Navigation
    public ExecutionRun? Run { get; set; }

    private ApprovalRequest() { } // EF

    public static ApprovalRequest Create(
        Guid runId, string actionType, string proposedAction,
        RiskLevel riskLevel, Guid? taskId = null)
    {
        return new ApprovalRequest
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            TaskId = taskId,
            ActionType = actionType,
            ProposedAction = proposedAction,
            RiskLevel = riskLevel,
            RequestedAt = DateTime.UtcNow,
        };
    }
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 10,
    Denied = 20,
    Expired = 30,
}

public enum RiskLevel
{
    Low = 0,
    Medium = 10,
    High = 20,
    Critical = 30,
}
