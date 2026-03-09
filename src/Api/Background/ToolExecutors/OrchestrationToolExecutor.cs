using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Orchestration;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd.Orchestration;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using Serilog;
using System.Text.Json;

namespace AzureOpsCrew.Api.Background.ToolExecutors;

/// <summary>
/// Executes orchestration tools (createTask, listTasks, postTaskProgress, completeTask, failTask).
/// These tools require DB access and richer context than simple backend tools.
/// </summary>
public class OrchestrationToolExecutor
{
    private readonly OrchestrationTaskService _taskService;

    public OrchestrationToolExecutor(OrchestrationTaskService taskService)
    {
        _taskService = taskService;
    }

    public async Task<AocFunctionResultContent> ExecuteTool(
        AgentRunData data,
        ToolDeclaration toolDeclaration,
        AocFunctionCallContent toolCall,
        CancellationToken ct)
    {
        try
        {
            return toolDeclaration.Name switch
            {
                OrchestrationTools.CreateTaskName => await ExecuteCreateTask(data, toolCall, ct),
                OrchestrationTools.ListTasksName => await ExecuteListTasks(data, toolCall, ct),
                OrchestrationTools.PostTaskProgressName => await ExecutePostTaskProgress(data, toolCall, ct),
                OrchestrationTools.CompleteTaskName => await ExecuteCompleteTask(data, toolCall, ct),
                OrchestrationTools.FailTaskName => await ExecuteFailTask(data, toolCall, ct),
                _ => AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId),
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[ORCHESTRATION] Error executing orchestration tool {ToolName}", toolDeclaration.Name);
            return new AocFunctionResultContent
            {
                CallId = toolCall.CallId,
                Result = new ToolCallResult(toolCall.CallId, new { ErrorMessage = ex.Message }, true),
            };
        }
    }

    private async Task<AocFunctionResultContent> ExecuteCreateTask(AgentRunData data, AocFunctionCallContent toolCall, CancellationToken ct)
    {
        var args = toolCall.Arguments ?? new Dictionary<string, object?>();
        var agentUsername = GetStringArg(args, "agentUsername");
        var title = GetStringArg(args, "title");
        var description = GetStringArg(args, "description");
        var announceInChat = GetBoolArg(args, "announceInChat", true);

        if (string.IsNullOrEmpty(agentUsername) || string.IsNullOrEmpty(title) || string.IsNullOrEmpty(description))
        {
            return ErrorResult(toolCall.CallId, "agentUsername, title, and description are required");
        }

        if (data.Channel == null)
        {
            return ErrorResult(toolCall.CallId, "createTask can only be used in a channel");
        }

        var task = await _taskService.CreateTaskAsync(
            data.Channel.Id, data.Agent.Id, agentUsername, title, description, announceInChat, ct);

        return SuccessResult(toolCall.CallId, new
        {
            taskId = task.Id.ToString(),
            assignedAgentUsername = agentUsername,
            status = task.Status.ToString(),
        });
    }

    private async Task<AocFunctionResultContent> ExecuteListTasks(AgentRunData data, AocFunctionCallContent toolCall, CancellationToken ct)
    {
        if (data.Channel == null)
        {
            return ErrorResult(toolCall.CallId, "listTasks can only be used in a channel");
        }

        var args = toolCall.Arguments ?? new Dictionary<string, object?>();
        var statusStr = GetStringArg(args, "status") ?? "All";

        OrchestrationTaskStatus? statusFilter = statusStr.ToLowerInvariant() switch
        {
            "pending" => OrchestrationTaskStatus.Pending,
            "inprogress" => OrchestrationTaskStatus.InProgress,
            "completed" => OrchestrationTaskStatus.Completed,
            "failed" => OrchestrationTaskStatus.Failed,
            _ => null, // "all" or anything else
        };

        var tasks = await _taskService.ListTasksAsync(data.Channel.Id, statusFilter, ct);

        // Resolve agent usernames for display
        var agentIds = tasks.Select(t => t.AssignedAgentId).Distinct().ToList();
        var agents = data.ParticipantAgents
            .Where(a => agentIds.Contains(a.Id))
            .ToDictionary(a => a.Id, a => a.Info.Username);

        var taskDtos = tasks.Select(t => new
        {
            taskId = t.Id.ToString(),
            title = t.Title,
            assignedAgent = agents.GetValueOrDefault(t.AssignedAgentId, "unknown"),
            status = t.Status.ToString(),
            progressSummary = t.ProgressSummary,
            resultSummary = t.ResultSummary,
            failureReason = t.FailureReason,
        }).ToList();

        return SuccessResult(toolCall.CallId, new { tasks = taskDtos });
    }

    private async Task<AocFunctionResultContent> ExecutePostTaskProgress(AgentRunData data, AocFunctionCallContent toolCall, CancellationToken ct)
    {
        var taskId = GetCurrentTaskId(data);
        if (taskId == null)
        {
            return ErrorResult(toolCall.CallId, "No current task assigned. postTaskProgress can only be used by a worker executing an assigned task.");
        }

        var args = toolCall.Arguments ?? new Dictionary<string, object?>();
        var message = GetStringArg(args, "message");
        var mirrorToChat = GetBoolArg(args, "mirrorToChat", true);

        if (string.IsNullOrEmpty(message))
        {
            return ErrorResult(toolCall.CallId, "message is required");
        }

        await _taskService.PostProgressAsync(taskId.Value, message, mirrorToChat, ct);

        return SuccessResult(toolCall.CallId, new { status = "progress_posted" });
    }

    private async Task<AocFunctionResultContent> ExecuteCompleteTask(AgentRunData data, AocFunctionCallContent toolCall, CancellationToken ct)
    {
        var taskId = GetCurrentTaskId(data);
        if (taskId == null)
        {
            return ErrorResult(toolCall.CallId, "No current task assigned. completeTask can only be used by a worker executing an assigned task.");
        }

        var args = toolCall.Arguments ?? new Dictionary<string, object?>();
        var result = GetStringArg(args, "result");
        var mirrorToChat = GetBoolArg(args, "mirrorToChat", true);

        if (string.IsNullOrEmpty(result))
        {
            return ErrorResult(toolCall.CallId, "result is required");
        }

        await _taskService.CompleteTaskAsync(taskId.Value, result, mirrorToChat, ct);

        return SuccessResult(toolCall.CallId, new { status = "completed" });
    }

    private async Task<AocFunctionResultContent> ExecuteFailTask(AgentRunData data, AocFunctionCallContent toolCall, CancellationToken ct)
    {
        var taskId = GetCurrentTaskId(data);
        if (taskId == null)
        {
            return ErrorResult(toolCall.CallId, "No current task assigned. failTask can only be used by a worker executing an assigned task.");
        }

        var args = toolCall.Arguments ?? new Dictionary<string, object?>();
        var reason = GetStringArg(args, "reason");
        var mirrorToChat = GetBoolArg(args, "mirrorToChat", true);

        if (string.IsNullOrEmpty(reason))
        {
            return ErrorResult(toolCall.CallId, "reason is required");
        }

        await _taskService.FailTaskAsync(taskId.Value, reason, mirrorToChat, ct);

        return SuccessResult(toolCall.CallId, new { status = "failed" });
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static Guid? GetCurrentTaskId(AgentRunData data)
    {
        return data.CurrentTask?.Id ?? data.Trigger?.TaskId;
    }

    private static string? GetStringArg(IDictionary<string, object?> args, string key)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
            return null;

        if (value is JsonElement je)
            return je.ValueKind == JsonValueKind.String ? je.GetString() : je.GetRawText();

        return value.ToString();
    }

    private static bool GetBoolArg(IDictionary<string, object?> args, string key, bool defaultValue)
    {
        if (!args.TryGetValue(key, out var value) || value == null)
            return defaultValue;

        if (value is JsonElement je)
            return je.ValueKind == JsonValueKind.True || (je.ValueKind == JsonValueKind.False ? false : defaultValue);

        if (value is bool b) return b;
        return defaultValue;
    }

    private static AocFunctionResultContent SuccessResult(string callId, object result)
    {
        return new AocFunctionResultContent
        {
            CallId = callId,
            Result = new ToolCallResult(callId, result, false),
        };
    }

    private static AocFunctionResultContent ErrorResult(string callId, string message)
    {
        return new AocFunctionResultContent
        {
            CallId = callId,
            Result = new ToolCallResult(callId, new { ErrorMessage = message }, true),
        };
    }
}
