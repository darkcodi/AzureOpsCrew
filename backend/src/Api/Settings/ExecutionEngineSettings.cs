namespace AzureOpsCrew.Api.Settings;

/// <summary>
/// Settings for the Task Execution Engine: budgets, timeouts, anti-loop limits.
/// Configurable via appsettings.json under "ExecutionEngine" section.
/// </summary>
public record ExecutionEngineSettings
{
    /// <summary>Maximum total steps per run before forced stop.</summary>
    public int MaxStepsPerRun { get; set; } = 50;

    /// <summary>Maximum steps per individual task before failing the task.</summary>
    public int MaxStepsPerTask { get; set; } = 15;

    /// <summary>Maximum number of replanning cycles per run.</summary>
    public int MaxReplans { get; set; } = 5;

    /// <summary>Maximum consecutive steps without progress before triggering anti-loop.</summary>
    public int MaxConsecutiveNonProgressSteps { get; set; } = 5;

    /// <summary>Timeout per individual task execution phase (seconds).</summary>
    public int TaskTimeoutSeconds { get; set; } = 120;

    /// <summary>Overall run timeout (minutes).</summary>
    public int RunTimeoutMinutes { get; set; } = 15;

    /// <summary>Maximum depth of task tree nesting.</summary>
    public int MaxTaskTreeDepth { get; set; } = 5;

    /// <summary>Maximum number of tasks per run.</summary>
    public int MaxTasksPerRun { get; set; } = 30;

    /// <summary>Maximum retry count per task.</summary>
    public int MaxRetriesPerTask { get; set; } = 2;

    /// <summary>
    /// Action types that always require user approval before execution.
    /// These are enforced at the engine level, not just in prompts.
    /// </summary>
    public List<string> ApprovalRequiredActions { get; set; } =
    [
        "deploy",
        "rollback",
        "restart_production",
        "delete_resource",
        "scale_production",
        "merge_to_main",
        "modify_production_config",
    ];

    /// <summary>
    /// Checkpoint types that will pause and save state.
    /// </summary>
    public List<string> CheckpointTypes { get; set; } =
    [
        "approval_checkpoint",
        "risk_checkpoint",
        "human_input_checkpoint",
        "replan_checkpoint",
        "verification_checkpoint",
    ];

    /// <summary>Confidence threshold below which replanning is suggested.</summary>
    public float ReplanConfidenceThreshold { get; set; } = 0.4f;

    /// <summary>Confidence threshold below which checkpoint is triggered.</summary>
    public float CheckpointConfidenceThreshold { get; set; } = 0.3f;
}
