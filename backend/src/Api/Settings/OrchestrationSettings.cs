namespace AzureOpsCrew.Api.Settings;

/// <summary>
/// Controls multi-agent orchestration behavior: max turns, timeouts, model temperatures.
/// All values configurable via appsettings.json under "Orchestration" section.
/// </summary>
public record OrchestrationSettings
{
    /// <summary>Max Manager↔Worker rounds per run (each round = Manager + delegated workers).</summary>
    public int MaxRoundsPerRun { get; set; } = 5;

    /// <summary>Max consecutive messages without any tool call before forcing stop.</summary>
    public int MaxConsecutiveNonToolTurns { get; set; } = 3;

    /// <summary>Idle timeout (seconds) after an agent finishes speaking. Stream closes if no new event arrives.</summary>
    public int IdleTimeoutSeconds { get; set; } = 45;

    /// <summary>Absolute timeout (minutes) for the entire run.</summary>
    public int TotalTimeoutMinutes { get; set; } = 5;

    /// <summary>Path to service-registry.yaml (relative to app base directory).</summary>
    public string? ServiceRegistryPath { get; set; }

    /// <summary>Per-role model settings keyed by providerAgentId (e.g. "manager", "devops", "developer").</summary>
    public Dictionary<string, AgentModelSettings> ModelSettings { get; set; } = new()
    {
        ["manager"] = new() { Temperature = 0.1f },
        ["devops"] = new() { Temperature = 0.05f },
        ["developer"] = new() { Temperature = 0.2f }
    };

    // ═══ FEATURE FLAGS (default enabled in dev) ═══

    /// <summary>
    /// Enable structured delegation via orchestrator_delegate_tasks tool call.
    /// When false, falls back to text-based name parsing.
    /// </summary>
    public bool EnableStructuredDelegation { get; set; } = true;

    /// <summary>
    /// Enable direct addressing (@DevOps, @Developer, @Manager) bypassing Manager coordination.
    /// </summary>
    public bool EnableDirectAddressing { get; set; } = true;

    /// <summary>
    /// Enable composite inventory tool that calls both Platform MCP and Azure MCP.
    /// </summary>
    public bool EnableCompositeInventoryTool { get; set; } = true;

    /// <summary>
    /// Enable artifact-first behavior for large tool outputs.
    /// </summary>
    public bool EnableArtifactFirst { get; set; } = true;

    /// <summary>
    /// Enable tool usage enforcement: reject worker responses without tool calls when required.
    /// </summary>
    public bool EnableToolEnforcement { get; set; } = true;

    // ═══ TOOL ENFORCEMENT SETTINGS ═══

    /// <summary>
    /// Max retries when a worker fails to use required tools (rejected + retry).
    /// After this limit, the task is marked as failed.
    /// </summary>
    public int MaxMissingToolRetries { get; set; } = 2;

    // ═══ ARTIFACT & CONTENT LIMITS ═══

    /// <summary>
    /// Threshold (in chars) above which tool results are stored as artifacts instead of inline.
    /// </summary>
    public int ToolInlineThresholdChars { get; set; } = 6000;

    /// <summary>
    /// Maximum chars for tool result inline in chat when artifact-first is disabled.
    /// </summary>
    public int MaxToolResultChars { get; set; } = 8000;

    // ═══ INVENTORY SETTINGS ═══

    /// <summary>
    /// Maximum pages to fetch during inventory pagination (safety limit).
    /// </summary>
    public int MaxInventoryPages { get; set; } = 50;

    /// <summary>
    /// Page size for inventory tool calls (if supported by MCP).
    /// </summary>
    public int InventoryPageSize { get; set; } = 100;
}

public record AgentModelSettings
{
    /// <summary>LLM temperature. Lower = more deterministic.</summary>
    public float Temperature { get; set; } = 0.1f;
}
