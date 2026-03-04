using System.Collections.Concurrent;
using Serilog;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Lightweight in-memory run context for tracking the current state of an orchestration run.
/// Not persisted — lives for the duration of a single AG-UI stream request.
/// Provides structured logging and step tracking.
/// </summary>
public class RunContext
{
    public string RunId { get; }
    public string ThreadId { get; }
    public Guid ChannelId { get; }
    public string UserRequest { get; }
    public RunStatus Status { get; private set; } = RunStatus.New;
    public string? Service { get; set; }
    public string? Environment { get; set; }
    public string? Severity { get; set; }
    public string? CurrentOwner { get; set; }
    public DateTime StartedAt { get; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; private set; }

    public List<RunLogEntry> Log { get; } = new();
    public List<string> Evidence { get; } = new();
    public List<string> Hypotheses { get; } = new();
    public List<string> ProposedActions { get; } = new();
    public List<string> ToolCalls { get; } = new();
    public int TotalTurns { get; private set; }
    public int ManagerTurns { get; private set; }
    public int NonToolTurns { get; private set; }
    public bool ApprovalRequested { get; set; }
    public bool ApprovalGranted { get; set; }

    // ═══ STRUCTURED DELEGATION ═══
    /// <summary>Queue of delegated tasks from orchestrator_delegate_tasks calls.</summary>
    private readonly ConcurrentQueue<(DelegatedTask Task, string TaskId)> _delegatedTaskQueue = new();
    
    /// <summary>Map of task ID to task result for tracking.</summary>
    private readonly ConcurrentDictionary<string, DelegatedTaskResult> _taskResults = new();
    
    /// <summary>Current task being executed by a worker.</summary>
    public (DelegatedTask Task, string TaskId)? CurrentTask { get; private set; }
    
    /// <summary>Number of retries for current task due to missing tool calls.</summary>
    public int CurrentTaskMissingToolRetries { get; private set; }

    // ═══ DIRECT ADDRESSING ═══
    /// <summary>Direct addressing metadata if user used @Agent syntax.</summary>
    public DirectAddressing? DirectAddress { get; set; }

    // ═══ METRICS ═══
    public int MissingToolRetryCount { get; private set; }
    public int InventorySourceCount { get; private set; }
    public int ArtifactsSaved { get; private set; }
    public int TruncationCount { get; private set; }

    public RunContext(string runId, string threadId, Guid channelId, string userRequest)
    {
        RunId = runId;
        ThreadId = threadId;
        ChannelId = channelId;
        UserRequest = userRequest;
    }

    // ═══ DELEGATED TASK MANAGEMENT ═══

    /// <summary>Queue a delegated task for execution.</summary>
    public void QueueDelegatedTask(DelegatedTask task, string taskId)
    {
        _delegatedTaskQueue.Enqueue((task, taskId));
        _taskResults[taskId] = new DelegatedTaskResult
        {
            TaskId = taskId,
            Assignee = task.Assignee,
            Intent = task.Intent,
            Status = DelegatedTaskStatus.Queued
        };

        Log.Add(new RunLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Event = $"Task queued: {taskId}",
            Detail = $"{task.Assignee}: {task.Goal}"
        });
    }

    /// <summary>Check if there are delegated tasks in the queue.</summary>
    public bool HasDelegatedTasks() => !_delegatedTaskQueue.IsEmpty || CurrentTask.HasValue;

    /// <summary>Dequeue the next delegated task for execution.</summary>
    public (DelegatedTask Task, string TaskId)? DequeueNextTask()
    {
        if (_delegatedTaskQueue.TryDequeue(out var task))
        {
            CurrentTask = task;
            CurrentTaskMissingToolRetries = 0;
            UpdateTaskStatus(task.TaskId, DelegatedTaskStatus.Running);
            return task;
        }
        return null;
    }

    /// <summary>Get task result by ID.</summary>
    public DelegatedTaskResult? GetTaskResult(string taskId) =>
        _taskResults.TryGetValue(taskId, out var result) ? result : null;

    /// <summary>Get all pending tasks (not yet completed/failed).</summary>
    public IEnumerable<(DelegatedTask Task, string TaskId)> GetPendingTasks() =>
        _delegatedTaskQueue.ToArray();

    /// <summary>Update task status and result.</summary>
    public void UpdateTaskStatus(string taskId, DelegatedTaskStatus status, string? summary = null, List<string>? toolsCalled = null, string? error = null)
    {
        if (_taskResults.TryGetValue(taskId, out var existing))
        {
            _taskResults[taskId] = existing with
            {
                Status = status,
                Summary = summary ?? existing.Summary,
                ToolsCalled = toolsCalled ?? existing.ToolsCalled,
                ErrorMessage = error ?? existing.ErrorMessage,
                RetryCount = CurrentTaskMissingToolRetries
            };
        }

        Log.Add(new RunLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Event = $"Task {taskId}: {status}",
            Detail = summary ?? error
        });
    }

    /// <summary>Complete the current task.</summary>
    public void CompleteCurrentTask(bool success, string? summary = null, List<string>? toolsCalled = null, string? error = null)
    {
        if (CurrentTask.HasValue)
        {
            var status = success ? DelegatedTaskStatus.Completed : DelegatedTaskStatus.Failed;
            UpdateTaskStatus(CurrentTask.Value.TaskId, status, summary, toolsCalled, error);
            CurrentTask = null;
            CurrentTaskMissingToolRetries = 0;
        }
    }

    /// <summary>Record a missing tool retry for current task.</summary>
    public void RecordMissingToolRetry()
    {
        CurrentTaskMissingToolRetries++;
        MissingToolRetryCount++;

        if (CurrentTask.HasValue)
        {
            UpdateTaskStatus(CurrentTask.Value.TaskId, DelegatedTaskStatus.RejectedNoTools,
                error: $"Missing tool call (retry {CurrentTaskMissingToolRetries})");
        }

        Log.Add(new RunLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Event = "Missing tool retry",
            Detail = $"Retry count: {CurrentTaskMissingToolRetries}"
        });
    }

    /// <summary>Increment inventory source count.</summary>
    public void RecordInventorySource() => InventorySourceCount++;

    /// <summary>Increment artifact saved count.</summary>
    public void RecordArtifactSaved() => ArtifactsSaved++;

    /// <summary>Increment truncation count.</summary>
    public void RecordTruncation() => TruncationCount++;

    /// <summary>Set direct addressing from user @Agent syntax.</summary>
    public void SetDirectAddress(DirectAddressing address)
    {
        DirectAddress = address;
        if (address.IsDirect)
        {
            Log.Add(new RunLogEntry
            {
                Timestamp = DateTime.UtcNow,
                Event = "Direct addressing",
                Detail = $"User addressed @{address.AddressedTo}"
            });
        }
    }

    public void TransitionTo(RunStatus newStatus, string? detail = null)
    {
        var oldStatus = Status;
        Status = newStatus;

        var entry = new RunLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Event = $"Status: {oldStatus} → {newStatus}",
            Detail = detail
        };
        Log.Add(entry);

        Serilog.Log.Information("[Run {RunId}] {Event} {Detail}", RunId, entry.Event, detail ?? "");

        if (newStatus is RunStatus.Resolved or RunStatus.Failed)
            CompletedAt = DateTime.UtcNow;
    }

    public void RecordAgentTurn(string agentName, bool usedTools)
    {
        TotalTurns++;
        if (agentName.Equals("Manager", StringComparison.OrdinalIgnoreCase))
            ManagerTurns++;

        if (!usedTools)
            NonToolTurns++;
        else
            NonToolTurns = 0; // Reset on tool use

        Log.Add(new RunLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Event = $"Agent turn: {agentName}",
            Detail = usedTools ? "used tools" : "no tools"
        });
    }

    public void RecordToolCall(string toolName, string agentName, bool success)
    {
        var entry = $"{agentName} → {toolName} ({(success ? "ok" : "failed")})";
        ToolCalls.Add(entry);
        Log.Add(new RunLogEntry
        {
            Timestamp = DateTime.UtcNow,
            Event = $"Tool call: {toolName}",
            Detail = $"by {agentName}, success={success}"
        });
    }

    public string ToSummary()
    {
        var duration = (CompletedAt ?? DateTime.UtcNow) - StartedAt;
        return $"""
            Run Summary:
              ID: {RunId}
              Status: {Status}
              Duration: {duration.TotalSeconds:F1}s
              Total turns: {TotalTurns}
              Tool calls: {ToolCalls.Count}
              Evidence items: {Evidence.Count}
              Hypotheses: {Hypotheses.Count}
              Approval requested: {ApprovalRequested}
              Approval granted: {ApprovalGranted}
              --- Metrics ---
              Missing tool retries: {MissingToolRetryCount}
              Inventory sources used: {InventorySourceCount}
              Artifacts saved: {ArtifactsSaved}
              Truncations: {TruncationCount}
              Direct address: {DirectAddress?.AddressedTo ?? "none"}
            """;
    }
}

public class RunLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Event { get; set; } = "";
    public string? Detail { get; set; }
}

public enum RunStatus
{
    New,
    Triaged,
    Investigating,
    WaitingForToolResult,
    WaitingForUserInput,
    WaitingForApproval,
    ImplementingFix,
    ReadyForPr,
    ReadyForDeploy,
    Deploying,
    Verifying,
    Resolved,
    Failed
}
