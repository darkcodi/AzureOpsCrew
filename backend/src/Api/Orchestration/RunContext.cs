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

    public RunContext(string runId, string threadId, Guid channelId, string userRequest)
    {
        RunId = runId;
        ThreadId = threadId;
        ChannelId = channelId;
        UserRequest = userRequest;
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
