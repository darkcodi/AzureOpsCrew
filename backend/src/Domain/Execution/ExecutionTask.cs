namespace AzureOpsCrew.Domain.Execution;

/// <summary>
/// A single task in the hierarchical task tree. Can be root or child.
/// Represents one unit of work: triage, investigate, fix, verify, etc.
/// </summary>
public class ExecutionTask
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid? ParentTaskId { get; set; }

    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public ExecutionTaskType TaskType { get; set; } = ExecutionTaskType.Generic;
    public string? AssignedAgent { get; set; }
    public int Priority { get; set; }

    public ExecutionTaskStatus Status { get; set; } = ExecutionTaskStatus.Created;

    // Dependencies (stored as comma-separated task IDs)
    public string? DependsOn { get; set; }

    // Goal and data
    public string? Goal { get; set; }
    public string? Inputs { get; set; } // JSON
    public string? ResultSummary { get; set; }

    // Budget per task
    public int StepCount { get; set; }
    public int RetryCount { get; set; }
    public int ReplanCount { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Navigation
    public ExecutionRun? Run { get; set; }
    public ExecutionTask? ParentTask { get; set; }
    public ICollection<ExecutionTask> ChildTasks { get; private set; } = new List<ExecutionTask>();
    public ICollection<Artifact> Artifacts { get; private set; } = new List<Artifact>();

    private ExecutionTask() { } // EF

    public static ExecutionTask Create(
        Guid runId, string title, ExecutionTaskType taskType, string? assignedAgent = null, Guid? parentTaskId = null)
    {
        var now = DateTime.UtcNow;
        return new ExecutionTask
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            ParentTaskId = parentTaskId,
            Title = title,
            TaskType = taskType,
            AssignedAgent = assignedAgent,
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public List<Guid> GetDependencyIds()
    {
        if (string.IsNullOrWhiteSpace(DependsOn)) return [];
        return DependsOn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Guid.Parse)
            .ToList();
    }

    public void SetDependencies(IEnumerable<Guid> taskIds)
    {
        DependsOn = string.Join(",", taskIds);
    }
}

public enum ExecutionTaskStatus
{
    Created = 0,
    Ready = 10,
    Blocked = 20,
    Running = 30,
    WaitingForTool = 40,
    WaitingForDependency = 50,
    WaitingForApproval = 60,
    WaitingForUserInput = 70,
    Succeeded = 100,
    Failed = 110,
    Cancelled = 120,
    Skipped = 130,
}

public enum ExecutionTaskType
{
    Generic = 0,
    RootGoal = 1,
    Triage = 10,
    Investigation = 20,
    EvidenceCollection = 25,
    Diagnosis = 30,
    Planning = 35,
    CodeFix = 40,
    PullRequest = 45,
    Approval = 50,
    Deployment = 60,
    Verification = 70,
    Rollback = 80,
    Handoff = 90,
    Summary = 100,
}
