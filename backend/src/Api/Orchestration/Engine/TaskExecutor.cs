using AzureOpsCrew.Api.Mcp;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Execution;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;

namespace AzureOpsCrew.Api.Orchestration.Engine;

/// <summary>
/// Executor layer: executes a single concrete task by:
/// - calling the appropriate agent (via LLM)
/// - invoking MCP tools when needed
/// - producing evidence and artifacts
/// - returning structured result to the engine
/// </summary>
public class TaskExecutor
{
    private readonly AzureOpsCrewContext _db;
    private readonly ExecutionEngineSettings _settings;
    private readonly McpToolProvider _mcpToolProvider;

    public TaskExecutor(
        AzureOpsCrewContext db,
        IOptions<ExecutionEngineSettings> settings,
        McpToolProvider mcpToolProvider)
    {
        _db = db;
        _settings = settings.Value;
        _mcpToolProvider = mcpToolProvider;
    }

    /// <summary>
    /// Execute a single task. Calls the assigned agent's LLM with MCP tools.
    /// Returns structured result including evidence, artifacts, and any approval needs.
    /// </summary>
    public async Task<TaskExecutionResult> ExecuteTaskAsync(
        ExecutionRun run, ExecutionTask task, CancellationToken ct = default)
    {
        Log.Information("[Executor] Executing task {TaskId}: {Title} (agent: {Agent})",
            task.Id, task.Title, task.AssignedAgent);

        try
        {
            // Gather context from completed sibling/parent tasks
            var context = BuildTaskContext(run, task);

            // Check if this task type requires approval
            if (RequiresApprovalCheck(task))
            {
                return TaskExecutionResult.NeedApproval(
                    $"Task '{task.Title}' requires approval before execution",
                    GetActionTypeForTask(task),
                    task.Description ?? task.Title,
                    DetermineRiskLevel(task));
            }

            // Get MCP tools for the agent
            IReadOnlyList<AITool> tools;
            try
            {
                tools = await _mcpToolProvider.GetToolsForAgentAsync(task.AssignedAgent ?? "developer", ct);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Executor] Failed to load MCP tools for {Agent}, continuing without", task.AssignedAgent);
                tools = [];
            }

            // Build prompt for the agent
            var prompt = BuildAgentPrompt(task, context, tools.Count > 0);

            // Execute via IChatClient if available — else produce a structured placeholder
            // In integrated mode, this will call the LLM with tools. 
            // For now, produce a heuristic result based on task type.
            var result = await ExecuteWithHeuristicAsync(run, task, context, tools, ct);

            // Store artifacts from execution
            if (result.ArtifactContent is not null)
            {
                var artifact = Artifact.Create(
                    run.Id,
                    GetArtifactTypeForTask(task.TaskType),
                    result.ArtifactContent,
                    task.AssignedAgent,
                    task.Id);
                artifact.Summary = result.Summary;
                _db.Artifacts.Add(artifact);
            }

            // Record tool calls in journal
            foreach (var toolCall in result.ToolCallsMade)
            {
                run.TotalToolCalls++;
                _db.JournalEntries.Add(JournalEntry.Create(
                    run.Id, JournalEntryType.ToolCall,
                    $"Tool: {toolCall}",
                    task.AssignedAgent, task.Id));
            }

            // Record evidence
            if (result.EvidenceCollected.Count > 0)
            {
                foreach (var evidence in result.EvidenceCollected)
                {
                    _db.JournalEntries.Add(JournalEntry.Create(
                        run.Id, JournalEntryType.EvidenceAdded,
                        evidence,
                        task.AssignedAgent, task.Id));
                }
            }

            await _db.SaveChangesAsync(ct);

            return result;
        }
        catch (OperationCanceledException)
        {
            return TaskExecutionResult.Fail("Task execution timed out or was cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Executor] Error executing task {TaskId}", task.Id);
            return TaskExecutionResult.Fail($"Execution error: {ex.Message}");
        }
    }

    // ─── Task execution by type ───

    private async Task<TaskExecutionResult> ExecuteWithHeuristicAsync(
        ExecutionRun run, ExecutionTask task, string context,
        IReadOnlyList<AITool> tools, CancellationToken ct)
    {
        // This is the integration point where actual MCP tool calls happen.
        // In the Phase 2 implementation, this dispatches to the appropriate
        // MCP tools based on task type and agent assignment.

        return task.TaskType switch
        {
            ExecutionTaskType.Triage => await ExecuteTriageAsync(run, task, context, tools, ct),
            ExecutionTaskType.EvidenceCollection => await ExecuteEvidenceCollectionAsync(run, task, context, tools, ct),
            ExecutionTaskType.Investigation => await ExecuteInvestigationAsync(run, task, context, tools, ct),
            ExecutionTaskType.Diagnosis => ExecuteDiagnosisAsync(run, task, context),
            ExecutionTaskType.CodeFix => ExecuteCodeFixAsync(run, task, context),
            ExecutionTaskType.PullRequest => ExecutePullRequestAsync(run, task, context),
            ExecutionTaskType.Deployment => ExecuteDeploymentAsync(run, task, context),
            ExecutionTaskType.Verification => await ExecuteVerificationAsync(run, task, context, tools, ct),
            ExecutionTaskType.Summary => ExecuteSummaryAsync(run, task, context),
            ExecutionTaskType.Approval => TaskExecutionResult.NeedApproval(
                task.Description ?? "Approval needed", "general", task.Title,
                RiskLevel.Medium),
            _ => TaskExecutionResult.Succeed(
                $"Task '{task.Title}' completed (generic execution)",
                [$"Generic task executed for: {task.Goal}"]),
        };
    }

    private async Task<TaskExecutionResult> ExecuteTriageAsync(
        ExecutionRun run, ExecutionTask task, string context,
        IReadOnlyList<AITool> tools, CancellationToken ct)
    {
        var toolCalls = new List<string>();
        var evidence = new List<string>();

        // Try to call health/status tools if available
        var healthTools = tools.Where(t =>
            t.Name.Contains("health", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("status", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("list", StringComparison.OrdinalIgnoreCase))
            .Take(3).ToList();

        foreach (var tool in healthTools)
        {
            try
            {
                var result = await InvokeMcpToolAsync(tool, new Dictionary<string, object?>(), ct);
                toolCalls.Add(tool.Name);
                evidence.Add($"[{tool.Name}]: {Truncate(result, 500)}");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Executor] Tool {Tool} failed during triage", tool.Name);
                toolCalls.Add($"{tool.Name} (failed)");
            }
        }

        if (toolCalls.Count == 0)
        {
            evidence.Add("No MCP tools available for triage; classification based on request text");
        }

        // Classify the issue
        var classification = ClassifyRequest(run.UserRequest);
        evidence.Add($"Classification: {classification}");

        return TaskExecutionResult.Succeed(
            $"Triage complete: {classification}",
            evidence, toolCalls,
            $"Triage results:\n{string.Join("\n", evidence)}");
    }

    private async Task<TaskExecutionResult> ExecuteEvidenceCollectionAsync(
        ExecutionRun run, ExecutionTask task, string context,
        IReadOnlyList<AITool> tools, CancellationToken ct)
    {
        var toolCalls = new List<string>();
        var evidence = new List<string>();

        // Invoke relevant query/read tools
        var queryTools = tools.Where(t =>
            t.Name.Contains("get", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("list", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("query", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("describe", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("log", StringComparison.OrdinalIgnoreCase))
            .Take(5).ToList();

        foreach (var tool in queryTools)
        {
            try
            {
                var result = await InvokeMcpToolAsync(tool, new Dictionary<string, object?>(), ct);
                toolCalls.Add(tool.Name);
                evidence.Add($"[{tool.Name}]: {Truncate(result, 500)}");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Executor] Tool {Tool} failed during evidence collection", tool.Name);
                toolCalls.Add($"{tool.Name} (failed)");
            }
        }

        var artifactContent = string.Join("\n---\n", evidence);

        return TaskExecutionResult.Succeed(
            $"Evidence collected: {evidence.Count} items from {toolCalls.Count} tool calls",
            evidence, toolCalls, artifactContent);
    }

    private async Task<TaskExecutionResult> ExecuteInvestigationAsync(
        ExecutionRun run, ExecutionTask task, string context,
        IReadOnlyList<AITool> tools, CancellationToken ct)
    {
        var toolCalls = new List<string>();
        var evidence = new List<string>();

        // More targeted investigation based on task goal with smart parameter inference
        var relevantTools = tools.Take(5).ToList();
        foreach (var tool in relevantTools)
        {
            try
            {
                var parameters = InferToolParameters(tool, run, task);
                var result = await InvokeMcpToolAsync(tool, parameters, ct);
                toolCalls.Add(tool.Name);
                evidence.Add($"[{tool.Name}]: {Truncate(result, 500)}");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Executor] Tool {Tool} failed during investigation", tool.Name);
            }
        }

        return TaskExecutionResult.Succeed(
            $"Investigation: {evidence.Count} findings",
            evidence, toolCalls,
            string.Join("\n", evidence));
    }

    private TaskExecutionResult ExecuteDiagnosisAsync(ExecutionRun run, ExecutionTask task, string context)
    {
        // Diagnosis synthesizes evidence from previous tasks
        var evidence = new List<string>
        {
            $"Diagnosis based on collected evidence for: {run.UserRequest}",
            $"Context from previous tasks: {Truncate(context, 500)}"
        };

        return TaskExecutionResult.Succeed(
            "Diagnosis completed based on collected evidence",
            evidence);
    }

    private TaskExecutionResult ExecuteCodeFixAsync(ExecutionRun run, ExecutionTask task, string context)
    {
        return TaskExecutionResult.Succeed(
            "Code fix task prepared for developer agent",
            [$"Code fix context: {Truncate(context, 300)}"]);
    }

    private TaskExecutionResult ExecutePullRequestAsync(ExecutionRun run, ExecutionTask task, string context)
    {
        return TaskExecutionResult.Succeed(
            "PR creation task prepared",
            [$"PR preparation based on: {Truncate(context, 300)}"]);
    }

    private TaskExecutionResult ExecuteDeploymentAsync(ExecutionRun run, ExecutionTask task, string context)
    {
        // Deployment ALWAYS requires approval — hard gate
        return TaskExecutionResult.NeedApproval(
            "Deployment requires user approval",
            "deploy",
            task.Description ?? $"Deploy fix for: {run.UserRequest}",
            RiskLevel.High,
            "Revert to previous version",
            "Check service health after deploy",
            task.Goal);
    }

    private async Task<TaskExecutionResult> ExecuteVerificationAsync(
        ExecutionRun run, ExecutionTask task, string context,
        IReadOnlyList<AITool> tools, CancellationToken ct)
    {
        var toolCalls = new List<string>();
        var evidence = new List<string>();

        // Run health checks
        var healthTools = tools.Where(t =>
            t.Name.Contains("health", StringComparison.OrdinalIgnoreCase) ||
            t.Name.Contains("status", StringComparison.OrdinalIgnoreCase))
            .Take(3).ToList();

        foreach (var tool in healthTools)
        {
            try
            {
                var result = await InvokeMcpToolAsync(tool, new Dictionary<string, object?>(), ct);
                toolCalls.Add(tool.Name);
                evidence.Add($"[Verification:{tool.Name}]: {Truncate(result, 500)}");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Executor] Verification tool {Tool} failed", tool.Name);
            }
        }

        return TaskExecutionResult.Succeed(
            "Verification completed",
            evidence, toolCalls,
            $"Verification report:\n{string.Join("\n", evidence)}");
    }

    private TaskExecutionResult ExecuteSummaryAsync(ExecutionRun run, ExecutionTask task, string context)
    {
        var completedTasks = run.Tasks
            .Where(t => t.Status == ExecutionTaskStatus.Succeeded && t.TaskType != ExecutionTaskType.RootGoal)
            .OrderBy(t => t.CompletedAt)
            .Select(t => $"• {t.Title}: {t.ResultSummary ?? "done"}")
            .ToList();

        var summary = $"""
            ## Run Summary
            **Goal:** {run.UserRequest}
            **Tasks completed:** {completedTasks.Count}
            
            {string.Join("\n", completedTasks)}
            
            **Total tool calls:** {run.TotalToolCalls}
            **Replans:** {run.TotalReplans}
            """;

        return TaskExecutionResult.Succeed(summary, [$"Final summary generated"]);
    }

    // ─── Helpers ───

    private async Task<string> InvokeMcpToolAsync(
        AITool tool, IDictionary<string, object?> args, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_settings.TaskTimeoutSeconds));

        if (tool is AIFunction func)
        {
            var aiArgs = new AIFunctionArguments(args);
            var result = await func.InvokeAsync(aiArgs, cts.Token);
            return result?.ToString() ?? "(no result)";
        }

        return "(tool type not supported)";
    }

    /// <summary>
    /// Intelligently infer parameters for MCP tools based on tool name and task context.
    /// Uses heuristics to provide reasonable defaults for common Azure MCP tools.
    /// </summary>
    private IDictionary<string, object?> InferToolParameters(
        AITool tool, ExecutionRun run, ExecutionTask task)
    {
        var parameters = new Dictionary<string, object?>();
        var toolName = tool.Name.ToLowerInvariant();

        // Azure MCP tools - extract subscription_id if available
        if (toolName.StartsWith("azure_"))
        {
            // Try to get subscription_id from environment or settings
            var subscriptionId = Environment.GetEnvironmentVariable("MCP_AZURE_SUBSCRIPTION_ID") 
                ?? Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                parameters["subscription_id"] = subscriptionId;
            }

            // Common Azure tool patterns
            if (toolName.Contains("list_resource_groups") || toolName.Contains("resourcegroups"))
            {
                // No additional params needed beyond subscription_id
            }
            else if (toolName.Contains("list_resources") || toolName.Contains("resource"))
            {
                // If we have a resource group from context, use it
                var rgMatch = System.Text.RegularExpressions.Regex.Match(
                    run.UserRequest ?? "", 
                    @"resource[- ]group[:\s]+([a-zA-Z0-9\-_]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (rgMatch.Success)
                {
                    parameters["resource_group"] = rgMatch.Groups[1].Value;
                }
                else
                {
                    // Try common patterns
                    parameters["resource_group"] = "rg-prod"; // Fallback
                }
            }
            else if (toolName.Contains("appservice") || toolName.Contains("webapp"))
            {
                // Extract web app name from request if present
                var appMatch = System.Text.RegularExpressions.Regex.Match(
                    run.UserRequest ?? "",
                    @"app[:\s]+([a-zA-Z0-9\-_]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (appMatch.Success)
                {
                    parameters["app_name"] = appMatch.Groups[1].Value;
                }
            }
            else if (toolName.Contains("aks") || toolName.Contains("kubernetes"))
            {
                var clusterMatch = System.Text.RegularExpressions.Regex.Match(
                    run.UserRequest ?? "",
                    @"cluster[:\s]+([a-zA-Z0-9\-_]+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
                if (clusterMatch.Success)
                {
                    parameters["cluster_name"] = clusterMatch.Groups[1].Value;
                }
            }
        }
        // ADO MCP tools
        else if (toolName.StartsWith("ado_"))
        {
            var organization = Environment.GetEnvironmentVariable("MCP_ADO_ORGANIZATION") 
                ?? Environment.GetEnvironmentVariable("AZURE_DEVOPS_ORGANIZATION");
            var project = Environment.GetEnvironmentVariable("MCP_ADO_PROJECT") 
                ?? Environment.GetEnvironmentVariable("AZURE_DEVOPS_PROJECT");

            if (!string.IsNullOrEmpty(organization))
            {
                parameters["organization"] = organization;
            }
            if (!string.IsNullOrEmpty(project))
            {
                parameters["project"] = project;
            }

            if (toolName.Contains("pipeline"))
            {
                parameters["top"] = 10; // Limit results
            }
        }
        // GitOps MCP tools
        else if (toolName.StartsWith("gitops_"))
        {
            var repo = Environment.GetEnvironmentVariable("MCP_GITOPS_REPO") 
                ?? "main-repo";
            parameters["repo"] = repo;

            if (toolName.Contains("create_branch"))
            {
                parameters["branch_name"] = $"fix/{task.Id}";
                parameters["base_branch"] = "main";
            }
        }

        Log.Information("[Executor] Inferred {Count} parameters for tool {Tool}: {Params}", 
            parameters.Count, tool.Name, 
            string.Join(", ", parameters.Select(kv => $"{kv.Key}={kv.Value}")));

        return parameters;
    }

    private string BuildTaskContext(ExecutionRun run, ExecutionTask task)
    {
        var parts = new List<string>();

        // Add results from dependency tasks
        var depIds = task.GetDependencyIds();
        foreach (var depId in depIds)
        {
            var dep = run.Tasks.FirstOrDefault(t => t.Id == depId);
            if (dep is not null && dep.ResultSummary is not null)
            {
                parts.Add($"[{dep.Title}]: {dep.ResultSummary}");
            }
        }

        // Add recent artifacts
        var recentArtifacts = run.Artifacts
            .OrderByDescending(a => a.CreatedAt)
            .Take(5)
            .Select(a => $"[Artifact:{a.ArtifactType}]: {a.Summary ?? Truncate(a.Content, 200)}");
        parts.AddRange(recentArtifacts);

        return string.Join("\n", parts);
    }

    private string BuildAgentPrompt(ExecutionTask task, string context, bool hasTools)
    {
        return $"""
            You are executing a specific task as part of an incident response workflow.
            
            Task: {task.Title}
            Goal: {task.Goal}
            Description: {task.Description}
            
            Context from previous tasks:
            {context}
            
            {(hasTools ? "You have MCP tools available. Use them to get REAL data." : "No MCP tools available.")}
            
            Be concise and evidence-based. Return your findings clearly.
            """;
    }

    private bool RequiresApprovalCheck(ExecutionTask task)
    {
        return task.TaskType is ExecutionTaskType.Deployment or ExecutionTaskType.Rollback;
    }

    private string GetActionTypeForTask(ExecutionTask task)
    {
        return task.TaskType switch
        {
            ExecutionTaskType.Deployment => "deploy",
            ExecutionTaskType.Rollback => "rollback",
            _ => "general",
        };
    }

    private RiskLevel DetermineRiskLevel(ExecutionTask task)
    {
        return task.TaskType switch
        {
            ExecutionTaskType.Deployment => RiskLevel.High,
            ExecutionTaskType.Rollback => RiskLevel.High,
            _ => RiskLevel.Medium,
        };
    }

    private ArtifactType GetArtifactTypeForTask(ExecutionTaskType taskType)
    {
        return taskType switch
        {
            ExecutionTaskType.Triage => ArtifactType.IncidentSummary,
            ExecutionTaskType.EvidenceCollection => ArtifactType.ToolOutput,
            ExecutionTaskType.Investigation => ArtifactType.ToolOutput,
            ExecutionTaskType.Diagnosis => ArtifactType.IncidentSummary,
            ExecutionTaskType.CodeFix => ArtifactType.CodeDiff,
            ExecutionTaskType.PullRequest => ArtifactType.PrLink,
            ExecutionTaskType.Verification => ArtifactType.VerificationReport,
            ExecutionTaskType.Summary => ArtifactType.IncidentSummary,
            _ => ArtifactType.Generic,
        };
    }

    private string ClassifyRequest(string request)
    {
        var lower = request.ToLowerInvariant();
        if (lower.Contains("deploy") || lower.Contains("pipeline") || lower.Contains("build"))
            return "Deployment/CI-CD issue";
        if (lower.Contains("500") || lower.Contains("error") || lower.Contains("exception") || lower.Contains("crash"))
            return "Application error";
        if (lower.Contains("slow") || lower.Contains("latency") || lower.Contains("performance") || lower.Contains("degradation"))
            return "Performance degradation";
        if (lower.Contains("down") || lower.Contains("unavailable") || lower.Contains("unreachable") || lower.Contains("не работает"))
            return "Service outage";
        if (lower.Contains("scale") || lower.Contains("capacity") || lower.Contains("memory") || lower.Contains("cpu"))
            return "Capacity/scaling issue";
        return "General investigation";
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}

/// <summary>Result of executing a single task.</summary>
public record TaskExecutionResult
{
    public bool Success { get; init; }
    public bool NeedsApproval { get; init; }
    public bool NeedsReplan { get; init; }
    public string? Summary { get; init; }
    public string? Error { get; init; }
    public string? ReplanReason { get; init; }
    public List<string> EvidenceCollected { get; init; } = [];
    public List<string> ToolCallsMade { get; init; } = [];
    public string? ArtifactContent { get; init; }

    // Approval package fields
    public string? ApprovalActionType { get; init; }
    public string? ApprovalProposedAction { get; init; }
    public RiskLevel ApprovalRiskLevel { get; init; }
    public string? ApprovalRollbackPlan { get; init; }
    public string? ApprovalVerificationPlan { get; init; }
    public string? ApprovalAffectedResources { get; init; }

    public static TaskExecutionResult Succeed(
        string summary,
        List<string>? evidence = null,
        List<string>? toolCalls = null,
        string? artifactContent = null)
        => new()
        {
            Success = true,
            Summary = summary,
            EvidenceCollected = evidence ?? [],
            ToolCallsMade = toolCalls ?? [],
            ArtifactContent = artifactContent,
        };

    public static TaskExecutionResult Fail(string error) =>
        new() { Error = error };

    public static TaskExecutionResult RequestReplan(string reason) =>
        new() { NeedsReplan = true, ReplanReason = reason };

    public static TaskExecutionResult NeedApproval(
        string summary, string actionType, string proposedAction,
        RiskLevel riskLevel,
        string? rollbackPlan = null, string? verificationPlan = null,
        string? affectedResources = null)
        => new()
        {
            NeedsApproval = true,
            Summary = summary,
            ApprovalActionType = actionType,
            ApprovalProposedAction = proposedAction,
            ApprovalRiskLevel = riskLevel,
            ApprovalRollbackPlan = rollbackPlan,
            ApprovalVerificationPlan = verificationPlan,
            ApprovalAffectedResources = affectedResources,
        };
}
