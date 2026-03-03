using Serilog;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Enforces strict tool authorization per agent role.
/// This is the CODE-LEVEL enforcement of the access matrix — not just prompt-level.
///
/// Access Matrix:
/// ┌───────────┬───────────────┬─────────────┬──────────────┬──────────────┐
/// │  Agent    │ Azure MCP     │ Platform MCP│ ADO MCP      │ GitOps MCP   │
/// ├───────────┼───────────────┼─────────────┼──────────────┼──────────────┤
/// │ Manager   │ read-only     │ read-only   │ read-only    │ NO ACCESS    │
/// │ DevOps    │ read+write    │ read+write  │ read-only    │ NO ACCESS    │
/// │ Developer │ NO ACCESS     │ NO ACCESS   │ read+ops     │ read+write   │
/// └───────────┴───────────────┴─────────────┴──────────────┴──────────────┘
///
/// Operation classes:
/// - read_safe: always allowed within role scope
/// - read_sensitive: allowed with logging (secrets metadata, sensitive configs)
/// - write_controlled: allowed for permitted agents + requires env-based policy
/// - write_dangerous: only through approval gate (prod deploy, prod config change, etc.)
/// </summary>
public static class ToolAuthorizationPolicy
{
    /// <summary>
    /// MCP server prefixes used in tool names.
    /// </summary>
    private static readonly string[] AzurePrefixes = ["azure_"];
    private static readonly string[] PlatformPrefixes = ["platform_"];
    private static readonly string[] AdoPrefixes = ["ado_"];
    private static readonly string[] GitOpsPrefixes = ["gitops_"];

    /// <summary>
    /// Keywords that indicate a write/mutation operation.
    /// </summary>
    private static readonly string[] WriteKeywords =
    [
        "create", "update", "delete", "remove", "modify", "set",
        "restart", "stop", "start", "scale", "deploy", "rollback",
        "trigger", "queue", "run", "merge", "approve", "commit",
        "push", "branch", "assign", "close", "reopen", "patch",
        "revoke", "rotate", "purge", "recover", "backup", "restore"
    ];

    /// <summary>
    /// Tool names or patterns known to be read-only even if they contain write-like words.
    /// For example "list_*", "get_*", "query_*", "search_*", "describe_*" are reads.
    /// </summary>
    private static readonly string[] ReadSafePrefixPatterns =
    [
        "list", "get", "query", "search", "describe", "show",
        "check", "health", "status", "count", "read", "fetch",
        "find", "lookup", "browse", "view", "inspect", "diagnose",
        "analyze", "monitor", "trace", "log"
    ];

    /// <summary>
    /// Determines the MCP server that a tool belongs to based on its name prefix.
    /// </summary>
    public static McpServerType GetServerType(string toolName)
    {
        var lower = toolName.ToLowerInvariant();
        if (AzurePrefixes.Any(p => lower.StartsWith(p))) return McpServerType.Azure;
        if (PlatformPrefixes.Any(p => lower.StartsWith(p))) return McpServerType.Platform;
        if (AdoPrefixes.Any(p => lower.StartsWith(p))) return McpServerType.Ado;
        if (GitOpsPrefixes.Any(p => lower.StartsWith(p))) return McpServerType.GitOps;
        return McpServerType.Unknown;
    }

    /// <summary>
    /// Returns true if the tool name indicates a write/mutation operation.
    /// </summary>
    public static bool IsWriteTool(string toolName)
    {
        var lower = toolName.ToLowerInvariant();

        // First check if it matches a known read-safe pattern
        // Strip the server prefix to get the action part
        var actionPart = StripServerPrefix(lower);

        foreach (var readPrefix in ReadSafePrefixPatterns)
        {
            if (actionPart.StartsWith(readPrefix))
                return false;
        }

        // Check for write keywords anywhere in the action
        foreach (var keyword in WriteKeywords)
        {
            if (actionPart.Contains(keyword))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the operation class for a given tool.
    /// </summary>
    public static OperationClass ClassifyOperation(string toolName)
    {
        if (!IsWriteTool(toolName))
        {
            // Check if it's sensitive (secrets, keys, passwords, tokens)
            var lower = toolName.ToLowerInvariant();
            if (lower.Contains("secret") || lower.Contains("key") || lower.Contains("password") ||
                lower.Contains("credential") || lower.Contains("token") || lower.Contains("certificate"))
                return OperationClass.ReadSensitive;

            return OperationClass.ReadSafe;
        }

        // Write operation — check if dangerous
        if (ApprovalPolicy.RequiresApproval(toolName))
            return OperationClass.WriteDangerous;

        return OperationClass.WriteControlled;
    }

    /// <summary>
    /// Validates whether the given agent is authorized to use a specific tool.
    /// Returns (authorized, reason).
    /// This is the HARD enforcement — called at tool invocation time as a guardrail.
    /// </summary>
    public static (bool Authorized, string? Reason) ValidateToolAccess(string agentRole, string toolName)
    {
        var role = agentRole.ToLowerInvariant();
        var server = GetServerType(toolName);
        var isWrite = IsWriteTool(toolName);

        switch (role)
        {
            case "manager":
                // Manager: read-only Azure + Platform + ADO. No GitOps. No writes.
                if (server == McpServerType.GitOps)
                    return (false, $"BLOCKED: Manager has no access to GitOps MCP. Tool '{toolName}' denied.");
                if (isWrite)
                    return (false, $"BLOCKED: Manager cannot execute write operations. Tool '{toolName}' is a write action.");
                return (true, null);

            case "devops":
                // DevOps: Azure (rw) + Platform (rw) + ADO (read-only). No GitOps.
                if (server == McpServerType.GitOps)
                    return (false, $"BLOCKED: DevOps has no access to GitOps MCP. Tool '{toolName}' denied. Code operations are Developer's responsibility.");
                if (server == McpServerType.Ado && isWrite)
                    return (false, $"BLOCKED: DevOps has read-only access to ADO MCP. Write tool '{toolName}' denied. Code changes are Developer's responsibility.");
                return (true, null);

            case "developer":
                // Developer: ADO (rw) + GitOps (rw). No Azure. No Platform.
                if (server == McpServerType.Azure)
                    return (false, $"BLOCKED: Developer has no access to Azure MCP. Tool '{toolName}' denied. Infrastructure operations are DevOps's responsibility.");
                if (server == McpServerType.Platform)
                    return (false, $"BLOCKED: Developer has no access to Platform MCP. Tool '{toolName}' denied. Infrastructure operations are DevOps's responsibility.");
                return (true, null);

            default:
                return (false, $"BLOCKED: Unknown agent role '{role}'. Tool '{toolName}' denied.");
        }
    }

    /// <summary>
    /// Validates and logs tool access. Returns the block reason if denied, null if allowed.
    /// Call this at tool invocation time as the ultimate guardrail.
    /// </summary>
    public static string? EnforceToolAccess(string agentRole, string toolName)
    {
        var (authorized, reason) = ValidateToolAccess(agentRole, toolName);
        if (!authorized)
        {
            Log.Warning("[ToolAuth] {Reason}", reason);
            return reason;
        }

        var opClass = ClassifyOperation(toolName);
        if (opClass == OperationClass.ReadSensitive)
        {
            Log.Information("[ToolAuth] Agent '{Agent}' accessing sensitive tool '{Tool}' — logging access", agentRole, toolName);
        }
        else if (opClass == OperationClass.WriteDangerous)
        {
            Log.Warning("[ToolAuth] Agent '{Agent}' attempting dangerous write '{Tool}' — approval policy will be checked", agentRole, toolName);
        }

        return null; // Allowed
    }

    private static string StripServerPrefix(string toolNameLower)
    {
        var idx = toolNameLower.IndexOf('_');
        return idx >= 0 && idx < toolNameLower.Length - 1
            ? toolNameLower[(idx + 1)..]
            : toolNameLower;
    }
}

public enum McpServerType
{
    Azure,
    Platform,
    Ado,
    GitOps,
    Unknown
}

public enum OperationClass
{
    /// <summary>Safe read operation — always allowed within role scope.</summary>
    ReadSafe,

    /// <summary>Read of sensitive data (secrets metadata, credentials) — allowed with logging.</summary>
    ReadSensitive,

    /// <summary>Write operation — allowed for permitted agents with env-based policy.</summary>
    WriteControlled,

    /// <summary>Dangerous write — requires explicit user approval via approval gate.</summary>
    WriteDangerous
}
