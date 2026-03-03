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
}

public record AgentModelSettings
{
    /// <summary>LLM temperature. Lower = more deterministic.</summary>
    public float Temperature { get; set; } = 0.1f;
}
