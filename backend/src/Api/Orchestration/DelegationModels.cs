using System.Text.Json.Serialization;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Represents a structured delegation request from Manager to workers.
/// Used with orchestrator_delegate_tasks tool.
/// </summary>
public record DelegationRequest
{
    [JsonPropertyName("tasks")]
    public List<DelegatedTask> Tasks { get; set; } = new();
}

/// <summary>
/// A single task in a delegation request.
/// Replaces text-based delegation with structured contract.
/// </summary>
public record DelegatedTask
{
    /// <summary>Target agent: "DevOps" or "Developer".</summary>
    [JsonPropertyName("assignee")]
    public string Assignee { get; set; } = "";

    /// <summary>Task intent/type: azure_inventory, ado_pipeline_status, code_fix, etc.</summary>
    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "";

    /// <summary>Human-readable goal description.</summary>
    [JsonPropertyName("goal")]
    public string Goal { get; set; } = "";

    /// <summary>If true, worker MUST use tool calls; text-only responses are rejected.</summary>
    [JsonPropertyName("requires_tools")]
    public bool RequiresTools { get; set; } = true;

    /// <summary>Specific tool names that should be used (for validation).</summary>
    [JsonPropertyName("required_tools")]
    public List<string> RequiredTools { get; set; } = new();

    /// <summary>Definition of done — what evidence/output is expected.</summary>
    [JsonPropertyName("definition_of_done")]
    public string? DefinitionOfDone { get; set; }

    /// <summary>Input artifact IDs from previous tasks.</summary>
    [JsonPropertyName("input_artifact_ids")]
    public List<string>? InputArtifactIds { get; set; }

    /// <summary>Additional context/inputs as JSON.</summary>
    [JsonPropertyName("inputs")]
    public Dictionary<string, object>? Inputs { get; set; }
}

/// <summary>
/// Standard intents for task classification.
/// Used to determine required tools and validation rules.
/// </summary>
public static class TaskIntents
{
    public const string AzureInventory = "azure_inventory";
    public const string AzureResourceHealth = "azure_resource_health";
    public const string AzureDiagnostics = "azure_diagnostics";
    public const string AzureRemediation = "azure_remediation";
    public const string AdoPipelineStatus = "ado_pipeline_status";
    public const string AdoWorkItems = "ado_work_items";
    public const string CodeAnalysis = "code_analysis";
    public const string CodeFix = "code_fix";
    public const string BranchCreation = "branch_creation";
    public const string PullRequestCreation = "pull_request_creation";
    public const string Verification = "verification";
    public const string Generic = "generic";
}

/// <summary>
/// Result of a delegated task execution.
/// </summary>
public record DelegatedTaskResult
{
    public required string TaskId { get; init; }
    public required string Assignee { get; init; }
    public required string Intent { get; init; }
    public required DelegatedTaskStatus Status { get; init; }
    public string? Summary { get; init; }
    public List<string>? ArtifactIds { get; init; }
    public List<string>? ToolsCalled { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryCount { get; init; }
}

public enum DelegatedTaskStatus
{
    Queued,
    Running,
    ToolCalled,
    Verified,
    Completed,
    Failed,
    RejectedNoTools
}

/// <summary>
/// Subtask creation request from one agent to another.
/// </summary>
public record SubtaskRequest
{
    [JsonPropertyName("assignee")]
    public string Assignee { get; set; } = "";

    [JsonPropertyName("intent")]
    public string Intent { get; set; } = "";

    [JsonPropertyName("goal")]
    public string Goal { get; set; } = "";

    [JsonPropertyName("requires_tools")]
    public bool RequiresTools { get; set; } = true;

    [JsonPropertyName("required_tools")]
    public List<string> RequiredTools { get; set; } = new();

    [JsonPropertyName("inputs")]
    public Dictionary<string, object>? Inputs { get; set; }

    [JsonPropertyName("input_artifact_ids")]
    public List<string>? InputArtifactIds { get; set; }

    [JsonPropertyName("definition_of_done")]
    public string? DefinitionOfDone { get; set; }
}

/// <summary>
/// Direct addressing metadata for @Agent routing.
/// </summary>
public record DirectAddressing
{
    /// <summary>The addressed agent: "DevOps", "Developer", "Manager", or null if not addressed.</summary>
    public string? AddressedTo { get; set; }

    /// <summary>The original message with the @Agent prefix removed.</summary>
    public string CleanedMessage { get; set; } = "";

    /// <summary>Whether this is a direct address bypass (skips Manager).</summary>
    public bool IsDirect => !string.IsNullOrEmpty(AddressedTo) && 
        !AddressedTo.Equals("Manager", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Result of inventory operation with source coverage.
/// </summary>
public record InventoryResult
{
    [JsonPropertyName("total_count")]
    public int TotalCount { get; set; }

    [JsonPropertyName("counts_by_type")]
    public Dictionary<string, int> CountsByType { get; set; } = new();

    [JsonPropertyName("counts_by_resource_group")]
    public Dictionary<string, int> CountsByResourceGroup { get; set; } = new();

    [JsonPropertyName("counts_by_subscription")]
    public Dictionary<string, int> CountsBySubscription { get; set; } = new();

    [JsonPropertyName("source_coverage")]
    public List<string> SourceCoverage { get; set; } = new();

    [JsonPropertyName("pagination_complete")]
    public bool PaginationComplete { get; set; }

    [JsonPropertyName("artifact_id")]
    public string? ArtifactId { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }
}

/// <summary>
/// Artifact fetch request parameters.
/// </summary>
public record ArtifactFetchRequest
{
    [JsonPropertyName("artifact_id")]
    public string ArtifactId { get; set; } = "";

    [JsonPropertyName("offset")]
    public int Offset { get; set; } = 0;

    [JsonPropertyName("limit")]
    public int Limit { get; set; } = 100;

    [JsonPropertyName("format")]
    public string Format { get; set; } = "json";
}

/// <summary>
/// Artifact fetch response with pagination info.
/// </summary>
public record ArtifactFetchResult
{
    [JsonPropertyName("artifact_id")]
    public required string ArtifactId { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("total_items")]
    public int TotalItems { get; init; }

    [JsonPropertyName("offset")]
    public int Offset { get; init; }

    [JsonPropertyName("limit")]
    public int Limit { get; init; }

    [JsonPropertyName("has_more")]
    public bool HasMore { get; init; }

    [JsonPropertyName("format")]
    public string Format { get; init; } = "json";
}
