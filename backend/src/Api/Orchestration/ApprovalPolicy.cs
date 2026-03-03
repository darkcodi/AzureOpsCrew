namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Defines which MCP/write operations require explicit user approval before execution.
/// Environment-aware: production operations always require approval; dev/incubator operations are logged but not blocked.
/// Read-only operations are always allowed.
/// </summary>
public static class ApprovalPolicy
{
    /// <summary>
    /// Prefixes of tool names that are considered dangerous (write/destructive).
    /// The system will request user approval before executing these in production.
    /// </summary>
    private static readonly string[] DangerousToolPrefixes =
    [
        // Azure resource mutations
        "azure_delete",
        "azure_restart",
        "azure_stop",
        "azure_start",
        "azure_scale",
        "azure_update",
        "azure_create",
        "azure_deploy",

        // Platform mutations
        "platform_delete",
        "platform_restart",
        "platform_update",
        "platform_create",
        "platform_deploy",
        "platform_scale",

        // Azure DevOps mutations
        "ado_trigger",
        "ado_create",
        "ado_update",
        "ado_delete",
        "ado_merge",
        "ado_approve",
        "ado_queue",
        "ado_run",

        // GitOps mutations
        "gitops_commit",
        "gitops_create_branch",
        "gitops_create_pr",
        "gitops_trigger",
        "gitops_merge",
        "gitops_delete",
    ];

    /// <summary>
    /// Keywords in tool names that indicate destructive operations.
    /// </summary>
    private static readonly string[] DangerousKeywords =
    [
        "deploy",
        "rollback",
        "restart",
        "delete",
        "remove",
        "scale",
        "merge",
        "approve",
        "trigger_pipeline",
        "queue_build",
        "commit_changes",
        "create_pr",
    ];

    /// <summary>
    /// Tool actions that ALWAYS require approval regardless of environment.
    /// These represent irreversible or high-impact operations.
    /// </summary>
    private static readonly string[] AlwaysRequiresApproval =
    [
        "delete",
        "merge",         // merging PRs to main/prod
        "deploy",        // production deploys
    ];

    /// <summary>
    /// Environment names considered "production" — require full approval gate.
    /// </summary>
    private static readonly string[] ProductionEnvironments =
    [
        "prod",
        "production",
        "prd",
        "main",
        "release",
    ];

    /// <summary>
    /// Environment names considered "non-production" — log intent, but don't block.
    /// </summary>
    private static readonly string[] NonProductionEnvironments =
    [
        "dev",
        "development",
        "incubator",
        "staging",
        "test",
        "sandbox",
        "preview",
    ];

    /// <summary>
    /// Checks if a tool invocation requires user approval.
    /// Does NOT consider environment — use RequiresApprovalForEnvironment for environment-aware checks.
    /// </summary>
    public static bool RequiresApproval(string toolName)
    {
        var lower = toolName.ToLowerInvariant();

        foreach (var prefix in DangerousToolPrefixes)
        {
            if (lower.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        foreach (var keyword in DangerousKeywords)
        {
            if (lower.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Environment-aware approval check.
    /// Returns an ApprovalDecision indicating whether the tool call should be blocked, logged, or allowed.
    /// </summary>
    /// <param name="toolName">The fully-qualified tool name (e.g. "azure_restart_container_app")</param>
    /// <param name="environment">The target environment name (e.g. "prod", "dev", "incubator"). Null = treat as prod.</param>
    public static ApprovalDecision RequiresApprovalForEnvironment(string toolName, string? environment)
    {
        if (!RequiresApproval(toolName))
            return ApprovalDecision.Allowed;

        var envLower = environment?.ToLowerInvariant()?.Trim() ?? "prod";

        // Check if action is in the "always requires approval" list (e.g. delete, merge to main)
        var lower = toolName.ToLowerInvariant();
        var alwaysBlock = AlwaysRequiresApproval.Any(k => lower.Contains(k, StringComparison.OrdinalIgnoreCase));
        if (alwaysBlock)
            return ApprovalDecision.RequiresApproval;

        // Production environments: always require approval for dangerous operations
        if (ProductionEnvironments.Any(e => envLower.Contains(e, StringComparison.OrdinalIgnoreCase)))
            return ApprovalDecision.RequiresApproval;

        // Non-production environments: log intent but allow execution
        if (NonProductionEnvironments.Any(e => envLower.Contains(e, StringComparison.OrdinalIgnoreCase)))
            return ApprovalDecision.LogAndProceed;

        // Unknown environment: default to requiring approval (safe default)
        return ApprovalDecision.RequiresApproval;
    }

    /// <summary>
    /// Returns true if the tool is a safe read-only operation.
    /// </summary>
    public static bool IsSafeReadOnly(string toolName)
    {
        return !RequiresApproval(toolName);
    }

    /// <summary>
    /// Wraps tool descriptions with an approval notice for dangerous tools.
    /// </summary>
    public static string EnhanceToolDescription(string toolName, string originalDescription)
    {
        if (RequiresApproval(toolName))
        {
            return $"[⚠️ REQUIRES USER APPROVAL] {originalDescription} " +
                   "This is a write/destructive operation. You MUST request explicit user approval " +
                   "before calling this tool. Present: what you want to do, why, risk assessment, " +
                   "and rollback plan. Then wait for the user to say 'APPROVED' before proceeding.";
        }

        return originalDescription;
    }

    /// <summary>
    /// Gets human-readable explanation of the approval requirement for a tool in a given environment.
    /// </summary>
    public static string GetApprovalExplanation(string toolName, string? environment)
    {
        var decision = RequiresApprovalForEnvironment(toolName, environment);
        return decision switch
        {
            ApprovalDecision.Allowed => $"Tool '{toolName}' is a read-only operation — no approval needed.",
            ApprovalDecision.RequiresApproval => $"Tool '{toolName}' is a write/destructive operation targeting '{environment ?? "unknown"}' environment — user approval REQUIRED.",
            ApprovalDecision.LogAndProceed => $"Tool '{toolName}' is a write operation targeting non-production environment '{environment}' — logging intent and proceeding.",
            _ => $"Tool '{toolName}' — unknown approval status."
        };
    }
}

/// <summary>
/// Result of an environment-aware approval check.
/// </summary>
public enum ApprovalDecision
{
    /// <summary>Tool is read-only / safe — no approval needed.</summary>
    Allowed,

    /// <summary>Tool is dangerous AND targets production — MUST get user approval before executing.</summary>
    RequiresApproval,

    /// <summary>Tool is dangerous but targets non-production — log intent and proceed without blocking.</summary>
    LogAndProceed
}
