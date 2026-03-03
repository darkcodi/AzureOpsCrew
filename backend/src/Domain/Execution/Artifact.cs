namespace AzureOpsCrew.Domain.Execution;

/// <summary>
/// A persisted artifact produced during task execution.
/// Tool outputs, logs, code diffs, PR links, diagnostics snapshots, etc.
/// Referenced by ID instead of being repeatedly pasted into chat.
/// </summary>
public class Artifact
{
    public Guid Id { get; private set; }
    public Guid RunId { get; private set; }
    public Guid? TaskId { get; set; }

    public ArtifactType ArtifactType { get; set; } = ArtifactType.Generic;
    public string? Source { get; set; }
    public string? CreatedBy { get; set; } // agent name
    public DateTime CreatedAt { get; private set; }

    /// <summary>
    /// The actual content or a reference (URL, path) to the content.
    /// For large content, store reference; for small content, store inline.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>Short human-readable summary of the artifact.</summary>
    public string? Summary { get; set; }

    /// <summary>Comma-separated tags for filtering.</summary>
    public string? Tags { get; set; }

    // Navigation
    public ExecutionRun? Run { get; set; }
    public ExecutionTask? Task { get; set; }

    private Artifact() { } // EF

    public static Artifact Create(
        Guid runId, ArtifactType type, string content, string? createdBy = null, Guid? taskId = null)
    {
        return new Artifact
        {
            Id = Guid.NewGuid(),
            RunId = runId,
            TaskId = taskId,
            ArtifactType = type,
            CreatedBy = createdBy,
            Content = content,
            CreatedAt = DateTime.UtcNow,
        };
    }
}

public enum ArtifactType
{
    Generic = 0,
    ToolOutput = 10,
    LogSnippet = 20,
    KqlResult = 25,
    HealthSnapshot = 30,
    DeploymentDiag = 35,
    CodeDiff = 40,
    BranchInfo = 45,
    PrLink = 50,
    VerificationReport = 60,
    IncidentSummary = 70,
    RollbackPlan = 80,
    RiskAssessment = 90,
    HandoffPackage = 100,
    ApprovalPackage = 110,
}
