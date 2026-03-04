using System.ComponentModel;
using System.Text.Json;
using AzureOpsCrew.Api.Mcp;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Execution;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Serilog;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Provides internal orchestrator tools that are not MCP-based.
/// These tools handle structured delegation, inventory fan-out, and artifact management.
/// </summary>
public class OrchestratorToolProvider
{
    private readonly OrchestrationSettings _settings;
    private readonly McpToolProvider _mcpToolProvider;
    private readonly Func<AzureOpsCrewContext> _dbContextFactory;
    private List<AITool>? _tools;

    public OrchestratorToolProvider(
        OrchestrationSettings settings,
        McpToolProvider mcpToolProvider,
        Func<AzureOpsCrewContext> dbContextFactory)
    {
        _settings = settings;
        _mcpToolProvider = mcpToolProvider;
        _dbContextFactory = dbContextFactory;
    }

    /// <summary>
    /// Gets all orchestrator tools for the Manager agent.
    /// Manager uses these tools for structured delegation instead of text-based mentions.
    /// </summary>
    public IReadOnlyList<AITool> GetManagerTools(RunContext runContext)
    {
        if (_tools is not null) return _tools;

        _tools = [];

        if (_settings.EnableStructuredDelegation)
        {
            _tools.Add(CreateDelegateTasksTool(runContext));
        }

        if (_settings.EnableCompositeInventoryTool)
        {
            _tools.Add(CreateInventoryListAllResourcesTool(runContext));
        }

        if (_settings.EnableArtifactFirst)
        {
            _tools.Add(CreateArtifactFetchTool(runContext));
        }

        // Stub for subtask creation (full implementation in next phase)
        _tools.Add(CreateSubtaskTool(runContext));

        return _tools;
    }

    /// <summary>
    /// Gets orchestrator tools for worker agents (DevOps, Developer).
    /// Workers get inventory and artifact tools but not delegation.
    /// </summary>
    public IReadOnlyList<AITool> GetWorkerTools(string agentRole, RunContext runContext)
    {
        var tools = new List<AITool>();

        if (_settings.EnableCompositeInventoryTool && 
            agentRole.Equals("devops", StringComparison.OrdinalIgnoreCase))
        {
            tools.Add(CreateInventoryListAllResourcesTool(runContext));
        }

        if (_settings.EnableArtifactFirst)
        {
            tools.Add(CreateArtifactFetchTool(runContext));
        }

        return tools;
    }

    private AITool CreateDelegateTasksTool(RunContext runContext)
    {
        return AIFunctionFactory.Create(
            name: "orchestrator_delegate_tasks",
            description: @"Delegate tasks to worker agents (DevOps or Developer). 
REQUIRED: Manager MUST use this tool to delegate tasks instead of mentioning agent names in text.
Each task should specify: assignee, intent, goal, requires_tools, required_tools, and definition_of_done.",
            method: ([Description("JSON object with 'tasks' array containing delegation specifications")] string tasksJson) =>
            {
                try
                {
                    var request = JsonSerializer.Deserialize<DelegationRequest>(tasksJson);
                    if (request?.Tasks == null || request.Tasks.Count == 0)
                    {
                        return JsonSerializer.Serialize(new { error = "No tasks provided in delegation request" });
                    }

                    var results = new List<DelegatedTaskResult>();
                    foreach (var task in request.Tasks)
                    {
                        // Validate assignee
                        if (!IsValidAssignee(task.Assignee))
                        {
                            results.Add(new DelegatedTaskResult
                            {
                                TaskId = Guid.NewGuid().ToString(),
                                Assignee = task.Assignee,
                                Intent = task.Intent,
                                Status = DelegatedTaskStatus.Failed,
                                ErrorMessage = $"Invalid assignee '{task.Assignee}'. Must be 'DevOps' or 'Developer'."
                            });
                            continue;
                        }

                        // Queue the task
                        var taskId = Guid.NewGuid().ToString();
                        runContext.QueueDelegatedTask(task, taskId);

                        results.Add(new DelegatedTaskResult
                        {
                            TaskId = taskId,
                            Assignee = task.Assignee,
                            Intent = task.Intent,
                            Status = DelegatedTaskStatus.Queued,
                            Summary = $"Task queued for {task.Assignee}: {task.Goal}"
                        });

                        Log.Information("[Run {RunId}] Delegated task {TaskId} to {Assignee}: {Intent}",
                            runContext.RunId, taskId, task.Assignee, task.Intent);
                    }

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        delegated_tasks = results,
                        message = $"Successfully queued {results.Count(r => r.Status == DelegatedTaskStatus.Queued)} tasks for execution"
                    });
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "[Run {RunId}] Failed to parse delegation request", runContext.RunId);
                    return JsonSerializer.Serialize(new { error = $"Invalid JSON format: {ex.Message}" });
                }
            });
    }

    private AITool CreateInventoryListAllResourcesTool(RunContext runContext)
    {
        return AIFunctionFactory.Create(
            name: "inventory_list_all_resources",
            description: @"List ALL Azure resources comprehensively by calling BOTH Platform MCP (Azure Resource Graph) AND Azure MCP.
This tool handles pagination automatically and stores large results as artifacts.
Returns: summary with counts, sourceCoverage, artifactId (if large), and whether pagination completed.
USE THIS instead of calling individual MCP tools for resource inventory tasks.",
            method: async (
                [Description("Optional subscription ID filter")] string? subscriptionId,
                [Description("Optional resource group filter")] string? resourceGroup,
                [Description("Optional resource type filter")] string? resourceType,
                CancellationToken ct) =>
            {
                var result = new InventoryResult();
                var allResources = new List<Dictionary<string, object>>();
                var warnings = new List<string>();

                try
                {
                    // 1. Call Platform MCP (ARG) for comprehensive inventory
                    var platformResources = await QueryPlatformMcpAsync(subscriptionId, resourceGroup, resourceType, ct);
                    if (platformResources.Count > 0)
                    {
                        allResources.AddRange(platformResources);
                        result.SourceCoverage.Add("Platform(ARG)");
                        Log.Information("[Run {RunId}] Platform MCP returned {Count} resources", 
                            runContext.RunId, platformResources.Count);
                    }
                    else
                    {
                        warnings.Add("Platform MCP returned no results or is unavailable");
                    }

                    // 2. Call Azure MCP for cross-reference
                    var azureResources = await QueryAzureMcpAsync(subscriptionId, resourceGroup, resourceType, ct);
                    if (azureResources.Count > 0)
                    {
                        // Merge by resourceId to avoid duplicates
                        var existingIds = allResources
                            .Where(r => r.ContainsKey("id"))
                            .Select(r => r["id"]?.ToString()?.ToLowerInvariant())
                            .ToHashSet();

                        foreach (var resource in azureResources)
                        {
                            var id = resource.ContainsKey("id") ? resource["id"]?.ToString()?.ToLowerInvariant() : null;
                            if (id != null && !existingIds.Contains(id))
                            {
                                allResources.Add(resource);
                                existingIds.Add(id);
                            }
                        }
                        result.SourceCoverage.Add("Azure");
                        Log.Information("[Run {RunId}] Azure MCP returned {Count} resources ({NewCount} unique)",
                            runContext.RunId, azureResources.Count, allResources.Count - platformResources.Count);
                    }
                    else
                    {
                        warnings.Add("Azure MCP returned no results or is unavailable");
                    }

                    result.TotalCount = allResources.Count;
                    result.PaginationComplete = true;

                    // 3. Calculate counts by type, resource group, subscription
                    foreach (var resource in allResources)
                    {
                        var type = resource.ContainsKey("type") ? resource["type"]?.ToString() ?? "unknown" : "unknown";
                        var rg = resource.ContainsKey("resourceGroup") ? resource["resourceGroup"]?.ToString() ?? "unknown" : "unknown";
                        var sub = resource.ContainsKey("subscriptionId") ? resource["subscriptionId"]?.ToString() ?? "unknown" : "unknown";

                        result.CountsByType[type] = result.CountsByType.GetValueOrDefault(type) + 1;
                        result.CountsByResourceGroup[rg] = result.CountsByResourceGroup.GetValueOrDefault(rg) + 1;
                        result.CountsBySubscription[sub] = result.CountsBySubscription.GetValueOrDefault(sub) + 1;
                    }

                    // 4. Store as artifact if large
                    var fullJson = JsonSerializer.Serialize(allResources, new JsonSerializerOptions { WriteIndented = true });
                    if (_settings.EnableArtifactFirst && fullJson.Length > _settings.ToolInlineThresholdChars)
                    {
                        var artifactId = await StoreArtifactAsync(runContext, fullJson, "inventory", ct);
                        result.ArtifactId = artifactId;
                        Log.Information("[Run {RunId}] Stored inventory as artifact {ArtifactId} ({Chars} chars)",
                            runContext.RunId, artifactId, fullJson.Length);
                    }

                    // 5. Build summary
                    result.Summary = $"Found {result.TotalCount} Azure resources across {result.CountsByResourceGroup.Count} resource groups. " +
                        $"Sources: {string.Join(", ", result.SourceCoverage)}. " +
                        (result.ArtifactId != null ? $"Full list stored as artifact {result.ArtifactId}." : "");

                    if (warnings.Count > 0)
                    {
                        result.Warnings = warnings;
                    }

                    return JsonSerializer.Serialize(result);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "[Run {RunId}] Inventory tool failed", runContext.RunId);
                    return JsonSerializer.Serialize(new
                    {
                        error = $"Inventory failed: {ex.Message}",
                        partial_results = result
                    });
                }
            });
    }

    private AITool CreateArtifactFetchTool(RunContext runContext)
    {
        return AIFunctionFactory.Create(
            name: "artifact_fetch",
            description: @"Fetch content from a stored artifact with pagination support.
Use this to retrieve large tool outputs that were stored as artifacts.
Supports offset/limit for paginated access without overloading context.",
            method: async (
                [Description("The artifact ID to fetch")] string artifactId,
                [Description("Offset (start position) for pagination, default 0")] int offset,
                [Description("Maximum items to return, default 100")] int limit,
                [Description("Output format: json, text, or table")] string format,
                CancellationToken ct) =>
            {
                try
                {
                    await using var dbContext = _dbContextFactory();
                    var artifact = await dbContext.Set<Artifact>()
                        .FirstOrDefaultAsync(a => a.Id == Guid.Parse(artifactId), ct);

                    if (artifact == null)
                    {
                        return JsonSerializer.Serialize(new { error = $"Artifact {artifactId} not found" });
                    }

                    // Parse content as JSON array if possible for pagination
                    var content = artifact.Content;
                    int totalItems = 1;
                    bool hasMore = false;

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(content);
                        if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            var array = jsonDoc.RootElement.EnumerateArray().ToList();
                            totalItems = array.Count;

                            if (offset >= totalItems)
                            {
                                return JsonSerializer.Serialize(new ArtifactFetchResult
                                {
                                    ArtifactId = artifactId,
                                    Content = "[]",
                                    TotalItems = totalItems,
                                    Offset = offset,
                                    Limit = limit,
                                    HasMore = false,
                                    Format = format
                                });
                            }

                            var slice = array.Skip(offset).Take(limit).ToList();
                            hasMore = offset + limit < totalItems;

                            content = JsonSerializer.Serialize(slice, new JsonSerializerOptions { WriteIndented = true });
                        }
                    }
                    catch
                    {
                        // Not a JSON array, return as-is with substring pagination
                        totalItems = content.Length;
                        if (offset < content.Length)
                        {
                            var endIndex = Math.Min(offset + limit * 100, content.Length);
                            content = content.Substring(offset, endIndex - offset);
                            hasMore = endIndex < totalItems;
                        }
                        else
                        {
                            content = "";
                            hasMore = false;
                        }
                    }

                    return JsonSerializer.Serialize(new ArtifactFetchResult
                    {
                        ArtifactId = artifactId,
                        Content = content,
                        TotalItems = totalItems,
                        Offset = offset,
                        Limit = limit,
                        HasMore = hasMore,
                        Format = format
                    });
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[Run {RunId}] Artifact fetch failed for {ArtifactId}", runContext.RunId, artifactId);
                    return JsonSerializer.Serialize(new { error = $"Failed to fetch artifact: {ex.Message}" });
                }
            });
    }

    private AITool CreateSubtaskTool(RunContext runContext)
    {
        return AIFunctionFactory.Create(
            name: "orchestrator_create_subtask",
            description: @"Create a subtask for another agent. Used for agent-to-agent task handoffs.
Example: DevOps identifies code issue and creates a subtask for Developer.
(Full implementation in next phase - currently returns stub acknowledgment)",
            method: ([Description("JSON object with subtask specification")] string subtaskJson) =>
            {
                try
                {
                    var request = JsonSerializer.Deserialize<SubtaskRequest>(subtaskJson);
                    if (request == null)
                    {
                        return JsonSerializer.Serialize(new { error = "Invalid subtask request" });
                    }

                    // Stub: acknowledge but don't fully execute yet
                    var subtaskId = Guid.NewGuid().ToString();
                    Log.Information("[Run {RunId}] Subtask created (stub): {SubtaskId} for {Assignee}",
                        runContext.RunId, subtaskId, request.Assignee);

                    return JsonSerializer.Serialize(new
                    {
                        success = true,
                        subtask_id = subtaskId,
                        assignee = request.Assignee,
                        intent = request.Intent,
                        status = "queued",
                        message = "Subtask queued for execution (stub implementation)"
                    });
                }
                catch (JsonException ex)
                {
                    return JsonSerializer.Serialize(new { error = $"Invalid JSON: {ex.Message}" });
                }
            });
    }

    private async Task<List<Dictionary<string, object>>> QueryPlatformMcpAsync(
        string? subscriptionId, string? resourceGroup, string? resourceType, CancellationToken ct)
    {
        var results = new List<Dictionary<string, object>>();

        if (!_mcpToolProvider.PlatformToolsAvailable)
        {
            Log.Warning("Platform MCP not available for inventory");
            return results;
        }

        try
        {
            var tools = await _mcpToolProvider.GetPlatformToolsAsync(ct);
            var argTool = tools.FirstOrDefault(t =>
                t.Name.Contains("arg", StringComparison.OrdinalIgnoreCase) &&
                t.Name.Contains("query", StringComparison.OrdinalIgnoreCase));

            if (argTool == null)
            {
                // Try alternative tool names
                argTool = tools.FirstOrDefault(t =>
                    t.Name.Contains("resource", StringComparison.OrdinalIgnoreCase) &&
                    t.Name.Contains("graph", StringComparison.OrdinalIgnoreCase));
            }

            if (argTool == null)
            {
                Log.Warning("No ARG query tool found in Platform MCP");
                return results;
            }

            // Build ARG query
            var query = "resources | project id, name, type, location, resourceGroup, subscriptionId, tags";
            if (!string.IsNullOrEmpty(resourceType))
            {
                query = $"resources | where type == '{resourceType.ToLowerInvariant()}' | project id, name, type, location, resourceGroup, subscriptionId, tags";
            }
            if (!string.IsNullOrEmpty(resourceGroup))
            {
                query = query.Replace("resources |", $"resources | where resourceGroup == '{resourceGroup}' |");
            }

            // Execute with pagination
            string? skipToken = null;
            int pageCount = 0;

            do
            {
                var args = new Dictionary<string, object?> { ["query"] = query };
                if (!string.IsNullOrEmpty(subscriptionId))
                {
                    args["subscriptions"] = new[] { subscriptionId };
                }
                if (!string.IsNullOrEmpty(skipToken))
                {
                    args["options"] = new { skipToken };
                }

                var result = await InvokeToolAsync(argTool, args, ct);
                if (result != null)
                {
                    var parsed = ParseResourceResult(result);
                    results.AddRange(parsed.resources);
                    skipToken = parsed.skipToken;
                }
                else
                {
                    break;
                }

                pageCount++;
            } while (!string.IsNullOrEmpty(skipToken) && pageCount < _settings.MaxInventoryPages);

            Log.Information("Platform MCP ARG query completed: {Count} resources in {Pages} pages",
                results.Count, pageCount);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Platform MCP query failed");
        }

        return results;
    }

    private async Task<List<Dictionary<string, object>>> QueryAzureMcpAsync(
        string? subscriptionId, string? resourceGroup, string? resourceType, CancellationToken ct)
    {
        var results = new List<Dictionary<string, object>>();

        if (!_mcpToolProvider.AzureToolsAvailable)
        {
            Log.Warning("Azure MCP not available for inventory");
            return results;
        }

        try
        {
            var tools = await _mcpToolProvider.GetAzureToolsAsync(ct);
            var listTool = tools.FirstOrDefault(t =>
                t.Name.Contains("list", StringComparison.OrdinalIgnoreCase) &&
                t.Name.Contains("resource", StringComparison.OrdinalIgnoreCase));

            if (listTool == null)
            {
                Log.Warning("No list resources tool found in Azure MCP");
                return results;
            }

            var args = new Dictionary<string, object?>();
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                args["subscriptionId"] = subscriptionId;
            }
            if (!string.IsNullOrEmpty(resourceGroup))
            {
                args["resourceGroup"] = resourceGroup;
            }
            if (!string.IsNullOrEmpty(resourceType))
            {
                args["resourceType"] = resourceType;
            }

            var result = await InvokeToolAsync(listTool, args, ct);
            if (result != null)
            {
                var parsed = ParseResourceResult(result);
                results.AddRange(parsed.resources);
            }

            Log.Information("Azure MCP list resources completed: {Count} resources", results.Count);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Azure MCP query failed");
        }

        return results;
    }

    private async Task<string?> InvokeToolAsync(AITool tool, Dictionary<string, object?> args, CancellationToken ct)
    {
        try
        {
            if (tool is AIFunction func)
            {
                // Convert Dictionary to AIFunctionArguments
                var aiArgs = new AIFunctionArguments(args);
                var result = await func.InvokeAsync(aiArgs, ct);
                return result?.ToString();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Tool invocation failed: {Tool}", tool.Name);
        }
        return null;
    }

    private (List<Dictionary<string, object>> resources, string? skipToken) ParseResourceResult(string result)
    {
        var resources = new List<Dictionary<string, object>>();
        string? skipToken = null;

        try
        {
            using var doc = JsonDocument.Parse(result);
            var root = doc.RootElement;

            // Handle different response formats
            JsonElement dataElement;

            if (root.TryGetProperty("data", out var data))
            {
                dataElement = data;
            }
            else if (root.TryGetProperty("value", out var value))
            {
                dataElement = value;
            }
            else if (root.ValueKind == JsonValueKind.Array)
            {
                dataElement = root;
            }
            else
            {
                return (resources, skipToken);
            }

            if (dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in dataElement.EnumerateArray())
                {
                    var resource = new Dictionary<string, object>();
                    foreach (var prop in item.EnumerateObject())
                    {
                        resource[prop.Name] = prop.Value.Clone();
                    }
                    resources.Add(resource);
                }
            }

            // Check for pagination tokens
            if (root.TryGetProperty("$skipToken", out var skip))
            {
                skipToken = skip.GetString();
            }
            else if (root.TryGetProperty("nextLink", out var nextLink))
            {
                // Extract skipToken from nextLink URL if present
                var next = nextLink.GetString();
                if (next?.Contains("$skipToken=") == true)
                {
                    var idx = next.IndexOf("$skipToken=") + 11;
                    skipToken = next.Substring(idx).Split('&')[0];
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse resource result");
        }

        return (resources, skipToken);
    }

    private async Task<string> StoreArtifactAsync(RunContext runContext, string content, string source, CancellationToken ct)
    {
        await using var dbContext = _dbContextFactory();

        var artifact = Artifact.Create(
            Guid.Parse(runContext.RunId),
            ArtifactType.ToolOutput,
            content,
            createdBy: "system");

        artifact.Source = source;
        artifact.Summary = $"Inventory result with {content.Length} chars";

        dbContext.Set<Artifact>().Add(artifact);
        await dbContext.SaveChangesAsync(ct);

        return artifact.Id.ToString();
    }

    private static bool IsValidAssignee(string assignee)
    {
        return assignee.Equals("DevOps", StringComparison.OrdinalIgnoreCase) ||
               assignee.Equals("Developer", StringComparison.OrdinalIgnoreCase);
    }
}
