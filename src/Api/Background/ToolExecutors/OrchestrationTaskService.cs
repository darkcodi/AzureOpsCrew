using AzureOpsCrew.Api.Background;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Orchestration;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AzureOpsCrew.Api.Background.ToolExecutors;

/// <summary>
/// Central service for orchestration task lifecycle: create, update status, mirror to chat, enqueue triggers.
/// </summary>
public class OrchestrationTaskService
{
    private readonly AzureOpsCrewContext _dbContext;
    private readonly AgentTriggerQueue _triggerQueue;
    private readonly IChannelEventBroadcaster? _channelEventBroadcaster;

    public OrchestrationTaskService(
        AzureOpsCrewContext dbContext,
        AgentTriggerQueue triggerQueue,
        IChannelEventBroadcaster? channelEventBroadcaster = null)
    {
        _dbContext = dbContext;
        _triggerQueue = triggerQueue;
        _channelEventBroadcaster = channelEventBroadcaster;
    }

    /// <summary>
    /// Creates a new task assigned to a worker agent.
    /// Called by manager via createTask tool.
    /// </summary>
    public async Task<OrchestrationTask> CreateTaskAsync(
        Guid channelId,
        Guid managerAgentId,
        string workerUsername,
        string title,
        string description,
        bool announceInChat,
        CancellationToken ct)
    {
        // Resolve worker agent by username within the channel
        var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct)
            ?? throw new InvalidOperationException($"Channel {channelId} not found");

        var workerAgent = await _dbContext.Agents
            .FirstOrDefaultAsync(a => channel.AgentIds.Contains(a.Id) && a.Info.Username == workerUsername, ct);

        // If EF can't filter by Username in the query (it's in a JSON column), do it in-memory
        if (workerAgent == null)
        {
            var channelAgents = await _dbContext.Agents
                .Where(a => channel.AgentIds.Contains(a.Id))
                .ToListAsync(ct);
            workerAgent = channelAgents.FirstOrDefault(a =>
                string.Equals(a.Info.Username, workerUsername, StringComparison.OrdinalIgnoreCase));
        }

        if (workerAgent == null)
        {
            throw new InvalidOperationException($"Agent with username '{workerUsername}' not found in channel '{channel.Name}'");
        }

        var task = new OrchestrationTask
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            CreatedByAgentId = managerAgentId,
            AssignedAgentId = workerAgent.Id,
            Title = title,
            Description = description,
            Status = OrchestrationTaskStatus.Pending,
            AnnounceInChat = announceInChat,
            CreatedAtUtc = DateTime.UtcNow,
        };

        _dbContext.OrchestrationTasks.Add(task);
        await _dbContext.SaveChangesAsync(ct);

        Log.Information("[ORCHESTRATION] Task created: {TaskId} '{Title}' assigned to {WorkerUsername} in channel {ChannelId}",
            task.Id, title, workerUsername, channelId);

        // Mirror manager announcement to chat if requested
        if (announceInChat)
        {
            var managerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == managerAgentId, ct);
            if (managerAgent != null)
            {
                var announcementText = $"Assigning task to **{workerUsername}**: {title}";
                await MirrorToChatAsync(channelId, managerAgent, announcementText, ct);
            }
        }

        // Enqueue worker with TaskAssigned trigger
        _triggerQueue.Enqueue(AgentTrigger.TaskAssigned(workerAgent.Id, channelId, task.Id));
        Log.Information("[ORCHESTRATION] Worker {WorkerAgentId} enqueued with TaskAssigned for task {TaskId}",
            workerAgent.Id, task.Id);

        return task;
    }

    /// <summary>
    /// Lists tasks for a channel, optionally filtered by status.
    /// </summary>
    public async Task<List<OrchestrationTask>> ListTasksAsync(
        Guid channelId,
        OrchestrationTaskStatus? statusFilter,
        CancellationToken ct)
    {
        var query = _dbContext.OrchestrationTasks
            .Where(t => t.ChannelId == channelId);

        if (statusFilter.HasValue)
        {
            query = query.Where(t => t.Status == statusFilter.Value);
        }

        return await query.OrderBy(t => t.CreatedAtUtc).ToListAsync(ct);
    }

    /// <summary>
    /// Posts a progress update for a task.
    /// Called by worker via postTaskProgress tool.
    /// </summary>
    public async Task PostProgressAsync(
        Guid taskId,
        string message,
        bool mirrorToChat,
        CancellationToken ct)
    {
        var task = await _dbContext.OrchestrationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        if (task.Status == OrchestrationTaskStatus.Pending)
        {
            task.Status = OrchestrationTaskStatus.InProgress;
            task.StartedAtUtc = DateTime.UtcNow;
        }

        task.ProgressSummary = message;
        await _dbContext.SaveChangesAsync(ct);

        Log.Information("[ORCHESTRATION] Task {TaskId} progress: {Message}", taskId, message);

        if (mirrorToChat)
        {
            var workerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == task.AssignedAgentId, ct);
            if (workerAgent != null)
            {
                await MirrorToChatAsync(task.ChannelId, workerAgent, message, ct);
            }
        }
    }

    /// <summary>
    /// Completes a task with a result summary.
    /// Called by worker via completeTask tool.
    /// </summary>
    public async Task CompleteTaskAsync(
        Guid taskId,
        string result,
        bool mirrorToChat,
        CancellationToken ct)
    {
        var task = await _dbContext.OrchestrationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        task.Status = OrchestrationTaskStatus.Completed;
        task.ResultSummary = result;
        task.CompletedAtUtc = DateTime.UtcNow;
        if (task.StartedAtUtc == null)
            task.StartedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        Log.Information("[ORCHESTRATION] Task {TaskId} completed: {Result}", taskId, result);

        if (mirrorToChat)
        {
            var workerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == task.AssignedAgentId, ct);
            if (workerAgent != null)
            {
                await MirrorToChatAsync(task.ChannelId, workerAgent, result, ct);
            }
        }

        // Re-trigger the manager
        var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == task.ChannelId, ct);
        if (channel?.ManagerAgentId != null)
        {
            _triggerQueue.Enqueue(AgentTrigger.TaskUpdated(channel.ManagerAgentId.Value, task.ChannelId, task.Id));
            Log.Information("[ORCHESTRATION] Manager {ManagerAgentId} re-triggered with TaskUpdated for task {TaskId}",
                channel.ManagerAgentId.Value, taskId);
        }
    }

    /// <summary>
    /// Fails a task with a reason.
    /// Called by worker via failTask tool.
    /// </summary>
    public async Task FailTaskAsync(
        Guid taskId,
        string reason,
        bool mirrorToChat,
        CancellationToken ct)
    {
        var task = await _dbContext.OrchestrationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");

        task.Status = OrchestrationTaskStatus.Failed;
        task.FailureReason = reason;
        task.FailedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        Log.Information("[ORCHESTRATION] Task {TaskId} failed: {Reason}", taskId, reason);

        if (mirrorToChat)
        {
            var workerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == task.AssignedAgentId, ct);
            if (workerAgent != null)
            {
                var failureText = $"Blocked: {reason}";
                await MirrorToChatAsync(task.ChannelId, workerAgent, failureText, ct);
            }
        }

        // Re-trigger the manager
        var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == task.ChannelId, ct);
        if (channel?.ManagerAgentId != null)
        {
            _triggerQueue.Enqueue(AgentTrigger.TaskUpdated(channel.ManagerAgentId.Value, task.ChannelId, task.Id));
            Log.Information("[ORCHESTRATION] Manager {ManagerAgentId} re-triggered with TaskUpdated for failed task {TaskId}",
                channel.ManagerAgentId.Value, taskId);
        }
    }

    /// <summary>
    /// Creates a visible message in the channel authored by the given agent.
    /// </summary>
    private async Task MirrorToChatAsync(Guid channelId, Agent agent, string text, CancellationToken ct)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = text,
            PostedAt = DateTime.UtcNow,
            AgentId = agent.Id,
            AuthorName = agent.Info.Username,
            ChannelId = channelId,
        };

        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync(ct);

        Log.Debug("[ORCHESTRATION] Mirrored message to channel {ChannelId} from agent {AgentUsername}: {Text}",
            channelId, agent.Info.Username, text.Length > 100 ? text[..100] + "…" : text);

        if (_channelEventBroadcaster != null)
        {
            await _channelEventBroadcaster.BroadcastMessageAddedAsync(channelId, message);
        }
    }
}
