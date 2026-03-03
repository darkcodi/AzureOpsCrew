namespace AzureOpsCrew.Domain.Execution;

/// <summary>
/// Root entity for a task execution run. One run per user request.
/// Contains the full task tree, plan history, and budget tracking.
/// </summary>
public class ExecutionRun
{
    public Guid Id { get; private set; }
    public Guid ChannelId { get; private set; }
    public int UserId { get; private set; }
    public string ThreadId { get; private set; } = "";
    public string UserRequest { get; private set; } = "";
    public string? Goal { get; set; }
    public string? Service { get; set; }
    public string? Environment { get; set; }
    public string? Severity { get; set; }

    public ExecutionRunStatus Status { get; set; } = ExecutionRunStatus.Created;

    // Plan tracking
    public string? InitialPlan { get; set; }
    public string? CurrentPlan { get; set; }
    public int PlanRevision { get; set; }
    public string? LastReplanReason { get; set; }

    // Budget tracking
    public int TotalSteps { get; set; }
    public int TotalToolCalls { get; set; }
    public int TotalReplans { get; set; }
    public int ConsecutiveNonProgressSteps { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Summary
    public string? ResultSummary { get; set; }
    public string? ErrorMessage { get; set; }

    // Navigation
    public ICollection<ExecutionTask> Tasks { get; private set; } = new List<ExecutionTask>();
    public ICollection<Artifact> Artifacts { get; private set; } = new List<Artifact>();
    public ICollection<JournalEntry> Journal { get; private set; } = new List<JournalEntry>();
    public ICollection<ApprovalRequest> ApprovalRequests { get; private set; } = new List<ApprovalRequest>();

    private ExecutionRun() { } // EF

    public static ExecutionRun Create(
        Guid channelId, int userId, string threadId, string userRequest)
    {
        var now = DateTime.UtcNow;
        return new ExecutionRun
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            UserId = userId,
            ThreadId = threadId,
            UserRequest = userRequest,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }
}

public enum ExecutionRunStatus
{
    Created = 0,
    Planning = 10,
    Running = 20,
    WaitingForApproval = 30,
    WaitingForUserInput = 40,
    Replanning = 50,
    Succeeded = 100,
    Failed = 110,
    Cancelled = 120,
    BudgetExhausted = 130,
    TimedOut = 140,
}
