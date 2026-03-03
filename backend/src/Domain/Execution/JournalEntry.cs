namespace AzureOpsCrew.Domain.Execution;

/// <summary>
/// Execution journal entry. The system of record for all decisions, observations, and state transitions.
/// Chat history is NOT the system of record; the journal is.
/// </summary>
public class JournalEntry
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid? TaskId { get; set; }

    public JournalEntryType EntryType { get; set; } = JournalEntryType.Info;
    public string? Agent { get; set; }
    public string Message { get; set; } = "";
    public string? Detail { get; set; } // JSON or longer text
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public ExecutionRun? Run { get; set; }

    private JournalEntry() { } // EF

    public static JournalEntry Create(
        Guid runId, JournalEntryType type, string message, string? agent = null, Guid? taskId = null, string? detail = null)
    {
        return new JournalEntry
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            TaskId = taskId,
            EntryType = type,
            Agent = agent,
            Message = message,
            Detail = detail,
            CreatedAt = DateTime.UtcNow,
        };
    }
}

public enum JournalEntryType
{
    Info = 0,
    StatusChange = 10,
    PlanCreated = 20,
    PlanRevised = 25,
    TaskCreated = 30,
    TaskStarted = 35,
    TaskCompleted = 40,
    TaskFailed = 45,
    ToolCall = 50,
    ToolResult = 55,
    EvidenceAdded = 60,
    HypothesisAdded = 65,
    HypothesisRejected = 70,
    HandoffSent = 80,
    HandoffReceived = 85,
    ApprovalRequested = 90,
    ApprovalGranted = 95,
    ApprovalDenied = 96,
    ReplanTriggered = 100,
    BudgetWarning = 110,
    BudgetExhausted = 115,
    Checkpoint = 120,
    Error = 200,
    Reflection = 210,
}
