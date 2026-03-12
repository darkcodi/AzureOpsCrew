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
    private sealed record WorkerCapabilityScore(
        Agent Agent,
        int TotalScore,
        int SpecializationScore,
        int ToolScore,
        int HeuristicScore,
        bool HasDeclaredCapability,
        string IntentSummary);

    private static readonly string[] InfraKeywords =
    [
        "azure", "resource", "resources", "subscription", "tenant", "rg", "resource group",
        "app service", "container app", "aks", "kubernetes", "vm", "vnet", "network", "storage", "key vault", "bicep", "terraform",
        "инфраструктур*", "ресурс*", "подписк*", "тенант*", "сеть*", "хранилищ*", "ключ*", "кластер*", "контейнер*", "виртуальн*"
    ];

    private static readonly string[] MonitoringKeywords =
    [
        "log", "logs", "monitor", "monitoring", "metrics", "alert", "alerts",
        "application insights", "app insights", "log analytics", "kql", "incident", "exception", "timeout", "latency",
        "лог*", "мониторинг*", "метрик*", "алерт*", "инцидент*", "исключен*", "таймаут*", "задержк*", "ошибк*"
    ];

    private static readonly string[] PipelineKeywords =
    [
        "pipeline", "pipelines", "ci", "cd", "ci/cd", "build", "release", "deployment",
        "deploy", "yaml", "repo", "repos", "branch", "pull request", "pr", "artifact",
        "пайплайн*", "конвейер*", "деплой*", "релиз*", "сборк*", "ветк*", "репозитор*", "артефакт*"
    ];

    private static readonly string[] CodeKeywords =
    [
        "code", "app code", "application code", "bug", "fix", "refactor", "module", "class", "function",
        "implementation", "config", "configuration", "source", "tests", "test",
        "код*", "исходн*", "конфиг*", "конфигурац*", "модул*", "класс*", "функц*", "рефактор*", "тест*"
    ];

    private enum PublicWorkerEventType
    {
        Started,
        Progress,
        Final,
    }

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
        string requestedWorkerUsername,
        string title,
        string description,
        bool announceInChat,
        CancellationToken ct)
    {
        // Resolve worker agent by username within the channel
        var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == channelId, ct)
            ?? throw new InvalidOperationException($"Channel {channelId} not found");
        if (!channel.IsOrchestrated || channel.ManagerAgentId == null)
        {
            throw new InvalidOperationException($"Channel '{channel.Name}' is not configured for manager orchestration.");
        }
        if (channel.ManagerAgentId != managerAgentId)
        {
            throw new InvalidOperationException("Only the configured manager can create orchestration tasks.");
        }

        var (workerAgent, routingReason, candidateScores) = await ResolveWorkerForTaskAsync(
            channel,
            managerAgentId,
            requestedWorkerUsername,
            title,
            description,
            ct);

        if (workerAgent.Id == managerAgentId)
        {
            throw new InvalidOperationException("Manager cannot assign orchestration tasks to itself.");
        }

        Log.Information(
            "[ORCHESTRATION] Capability routing decision: channel {ChannelId}, requested {RequestedWorker}, selected {SelectedWorker}, reason: {Reason}, taskTitle: {TaskTitle}, scores: {CandidateScores}",
            channelId,
            requestedWorkerUsername,
            workerAgent.Info.Username,
            routingReason,
            title,
            string.Join(" | ", candidateScores.Select(s =>
                $"{s.Agent.Info.Username}:total={s.TotalScore},spec={s.SpecializationScore},tools={s.ToolScore},heur={s.HeuristicScore},declared={s.HasDeclaredCapability},intent={s.IntentSummary}")));

        EnsureWorkerHasDeclaredCapability(channelId, workerAgent, title, description, routingReason);

        // Practical guardrail: do not create duplicate active tasks with the same owner+intent.
        var duplicateTask = await FindDuplicateActiveTaskAsync(channelId, workerAgent.Id, title, description, ct);
        if (duplicateTask != null)
        {
            Log.Information(
                "[ORCHESTRATION] Suppressed duplicate task creation in channel {ChannelId}. Existing task {TaskId} already assigned to worker {WorkerAgentId}",
                channelId, duplicateTask.Id, workerAgent.Id);

            _triggerQueue.Enqueue(AgentTrigger.TaskAssigned(workerAgent.Id, channelId, duplicateTask.Id));
            Log.Information("[ORCHESTRATION] Re-enqueued worker {WorkerAgentId} for existing task {TaskId} (duplicate assignment suppressed)",
                workerAgent.Id, duplicateTask.Id);

            return duplicateTask;
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

        Log.Information(
            "[ORCHESTRATION] Task created: {TaskId} '{Title}' assigned to {WorkerUsername} ({WorkerAgentId}) by manager {ManagerAgentId} in channel {ChannelId}",
            task.Id, title, workerAgent.Info.Username, workerAgent.Id, managerAgentId, channelId);

        // Mirror manager announcement to chat if requested
        if (announceInChat)
        {
                var managerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == managerAgentId, ct);
            if (managerAgent != null)
            {
                var announcementText = $"Assigning task to **{workerAgent.Info.Username}**: {title}";
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
        Guid workerAgentId,
        string message,
        bool mirrorToChat,
        CancellationToken ct)
    {
        var task = await _dbContext.OrchestrationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");
        if (task.AssignedAgentId != workerAgentId)
        {
            Log.Warning("[ORCHESTRATION] Blocked progress update from non-owner worker {WorkerAgentId} for task {TaskId}. Owner is {OwnerAgentId}",
                workerAgentId, taskId, task.AssignedAgentId);
            throw new InvalidOperationException("Only the assigned worker can post progress for this task.");
        }
        if (task.Status is OrchestrationTaskStatus.Completed or OrchestrationTaskStatus.Failed)
        {
            Log.Warning("[ORCHESTRATION] Blocked progress update for terminal task {TaskId} ({Status})",
                task.Id, task.Status);
            throw new InvalidOperationException("Cannot post progress for a completed or failed task.");
        }

        if (task.Status == OrchestrationTaskStatus.Pending)
        {
            task.Status = OrchestrationTaskStatus.InProgress;
            task.StartedAtUtc = DateTime.UtcNow;
        }

        task.ProgressSummary = message;
        await _dbContext.SaveChangesAsync(ct);

        Log.Information("[ORCHESTRATION] Task {TaskId} progress: {Message}", taskId, message);

        if (mirrorToChat && task.AnnounceInChat)
        {
            var workerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == task.AssignedAgentId, ct);
            if (workerAgent != null)
            {
                await TryMirrorWorkerProgressAsync(task, workerAgent, message, ct);
            }
        }
    }

    /// <summary>
    /// Completes a task with a result summary.
    /// Called by worker via completeTask tool.
    /// </summary>
    public async Task CompleteTaskAsync(
        Guid taskId,
        Guid workerAgentId,
        string result,
        bool mirrorToChat,
        CancellationToken ct)
    {
        var task = await _dbContext.OrchestrationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");
        if (task.AssignedAgentId != workerAgentId)
        {
            Log.Warning("[ORCHESTRATION] Blocked completion from non-owner worker {WorkerAgentId} for task {TaskId}. Owner is {OwnerAgentId}",
                workerAgentId, taskId, task.AssignedAgentId);
            throw new InvalidOperationException("Only the assigned worker can complete this task.");
        }
        if (task.Status is OrchestrationTaskStatus.Completed or OrchestrationTaskStatus.Failed)
        {
            Log.Warning("[ORCHESTRATION] Blocked completion for terminal task {TaskId} ({Status})",
                task.Id, task.Status);
            throw new InvalidOperationException($"Task is already terminal ({task.Status}).");
        }

        task.Status = OrchestrationTaskStatus.Completed;
        task.ResultSummary = result;
        task.CompletedAtUtc = DateTime.UtcNow;
        if (task.StartedAtUtc == null)
            task.StartedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        Log.Information("[ORCHESTRATION] Task {TaskId} completed: {Result}", taskId, result);

        if (mirrorToChat && task.AnnounceInChat)
        {
            var workerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == task.AssignedAgentId, ct);
            if (workerAgent != null)
            {
                await TryMirrorWorkerFinalAsync(task, workerAgent, result, ct);
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
        Guid workerAgentId,
        string reason,
        bool mirrorToChat,
        CancellationToken ct)
    {
        var task = await _dbContext.OrchestrationTasks.FirstOrDefaultAsync(t => t.Id == taskId, ct)
            ?? throw new InvalidOperationException($"Task {taskId} not found");
        if (task.AssignedAgentId != workerAgentId)
        {
            Log.Warning("[ORCHESTRATION] Blocked failure update from non-owner worker {WorkerAgentId} for task {TaskId}. Owner is {OwnerAgentId}",
                workerAgentId, taskId, task.AssignedAgentId);
            throw new InvalidOperationException("Only the assigned worker can fail this task.");
        }
        if (task.Status is OrchestrationTaskStatus.Completed or OrchestrationTaskStatus.Failed)
        {
            Log.Warning("[ORCHESTRATION] Blocked failure update for terminal task {TaskId} ({Status})",
                task.Id, task.Status);
            throw new InvalidOperationException($"Task is already terminal ({task.Status}).");
        }

        task.Status = OrchestrationTaskStatus.Failed;
        task.FailureReason = reason;
        task.FailedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        Log.Information("[ORCHESTRATION] Task {TaskId} failed: {Reason}", taskId, reason);

        if (mirrorToChat && task.AnnounceInChat)
        {
            var workerAgent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == task.AssignedAgentId, ct);
            if (workerAgent != null)
            {
                var failureText = $"Blocked: {reason}";
                await TryMirrorWorkerFinalAsync(task, workerAgent, failureText, ct);
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

    private async Task<OrchestrationTask?> FindDuplicateActiveTaskAsync(
        Guid channelId,
        Guid workerAgentId,
        string title,
        string description,
        CancellationToken ct)
    {
        var activeTasks = await _dbContext.OrchestrationTasks
            .Where(t =>
                t.ChannelId == channelId &&
                t.AssignedAgentId == workerAgentId &&
                (t.Status == OrchestrationTaskStatus.Pending || t.Status == OrchestrationTaskStatus.InProgress))
            .OrderByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        var incomingCombined = $"{title}\n{description}";
        var incomingIntent = BuildIntentSignature(incomingCombined);

        return activeTasks.FirstOrDefault(t =>
            IsNearDuplicateText(t.Title, title)
            || IsNearDuplicateText($"{t.Title}\n{t.Description}", incomingCombined)
            || (incomingIntent != "generic"
                && string.Equals(
                    BuildIntentSignature($"{t.Title}\n{t.Description}"),
                    incomingIntent,
                    StringComparison.Ordinal)));
    }

    private static void EnsureWorkerHasDeclaredCapability(
        Guid channelId,
        Agent workerAgent,
        string title,
        string description,
        string routingReason)
    {
        var hasDescription = !string.IsNullOrWhiteSpace(workerAgent.Info.Description);
        var hasBoundTools = workerAgent.Info.AvailableMcpServerTools.Length > 0;

        if (!hasDescription && !hasBoundTools)
        {
            throw new InvalidOperationException(
                $"Worker '{workerAgent.Info.Username}' has no declared specialization or bound tools and cannot be assigned orchestration tasks.");
        }

        Log.Information(
            "[ORCHESTRATION] Delegation capability check: channel {ChannelId}, worker {WorkerUsername} ({WorkerAgentId}), hasDescription: {HasDescription}, boundMcpCount: {BoundMcpCount}, taskTitle: {TaskTitle}, taskDescriptionPreview: {TaskDescriptionPreview}",
            channelId,
            workerAgent.Info.Username,
            workerAgent.Id,
            hasDescription,
            workerAgent.Info.AvailableMcpServerTools.Length,
            title,
            TruncateForLog(description));

        Log.Information(
            "[ORCHESTRATION] Delegation routing rationale: channel {ChannelId}, worker {WorkerUsername}, reason: {RoutingReason}",
            channelId,
            workerAgent.Info.Username,
            routingReason);
    }

    private async Task<(Agent Worker, string Reason, List<WorkerCapabilityScore> Scores)> ResolveWorkerForTaskAsync(
        Domain.Channels.Channel channel,
        Guid managerAgentId,
        string requestedWorkerUsername,
        string title,
        string description,
        CancellationToken ct)
    {
        var channelAgents = await _dbContext.Agents
            .Where(a => channel.AgentIds.Contains(a.Id))
            .ToListAsync(ct);

        var workers = channelAgents
            .Where(a => a.Id != managerAgentId)
            .ToList();

        if (workers.Count == 0)
            throw new InvalidOperationException("No worker agents are available in this channel.");

        var taskText = $"{title}\n{description}";
        var scores = workers
            .Select(worker => ScoreWorkerCapability(worker, title, description, requestedWorkerUsername))
            .OrderByDescending(s => s.TotalScore)
            .ThenByDescending(s => s.ToolScore)
            .ThenByDescending(s => s.SpecializationScore)
            .ToList();

        var requestedWorker = workers.FirstOrDefault(a =>
            string.Equals(a.Info.Username, requestedWorkerUsername, StringComparison.OrdinalIgnoreCase));

        var best = scores.First();
        if (requestedWorker == null)
        {
            return (
                best.Agent,
                $"requested worker '{requestedWorkerUsername}' was not found; auto-selected highest capability score",
                scores);
        }

        var requestedScore = scores.First(s => s.Agent.Id == requestedWorker.Id);
        var rerouteThreshold = 15;
        var shouldReroute = best.Agent.Id != requestedWorker.Id &&
                            best.TotalScore >= requestedScore.TotalScore + rerouteThreshold;

        if (shouldReroute)
        {
            return (
                best.Agent,
                $"requested worker '{requestedWorker.Info.Username}' capability score ({requestedScore.TotalScore}) is below better candidate '{best.Agent.Info.Username}' ({best.TotalScore})",
                scores);
        }

        return (
            requestedWorker,
            $"kept requested worker '{requestedWorker.Info.Username}' (score {requestedScore.TotalScore}); best alternative '{best.Agent.Info.Username}' score {best.TotalScore}",
            scores);
    }

    private static WorkerCapabilityScore ScoreWorkerCapability(
        Agent worker,
        string title,
        string description,
        string requestedWorkerUsername)
    {
        var normalizedTitle = NormalizeForCapability(title);
        var normalizedDescription = NormalizeForCapability(description);

        var agentProfile = NormalizeForCapability(string.Join(
            " ",
            worker.Info.Username,
            worker.Info.Description ?? string.Empty,
            worker.Info.Prompt ?? string.Empty));

        var toolNames = worker.Info.AvailableMcpServerTools
            .SelectMany(x => x.EnabledToolNames ?? Array.Empty<string>())
            .Select(NormalizeForCapability)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        var titleInfraIntent = HasAnyKeyword(normalizedTitle, InfraKeywords);
        var titleMonitoringIntent = HasAnyKeyword(normalizedTitle, MonitoringKeywords);
        var titlePipelineIntent = HasAnyKeyword(normalizedTitle, PipelineKeywords);
        var titleCodeIntent = HasAnyKeyword(normalizedTitle, CodeKeywords);
        var hasTitleSignal = titleInfraIntent || titleMonitoringIntent || titlePipelineIntent || titleCodeIntent;

        var descriptionInfraIntent = HasAnyKeyword(normalizedDescription, InfraKeywords);
        var descriptionMonitoringIntent = HasAnyKeyword(normalizedDescription, MonitoringKeywords);
        var descriptionPipelineIntent = HasAnyKeyword(normalizedDescription, PipelineKeywords);
        var descriptionCodeIntent = HasAnyKeyword(normalizedDescription, CodeKeywords);

        // Prioritize title intent to reduce noisy description-driven reroutes.
        var infraIntent = hasTitleSignal ? titleInfraIntent : descriptionInfraIntent;
        var monitoringIntent = hasTitleSignal ? titleMonitoringIntent : descriptionMonitoringIntent;
        var pipelineIntent = hasTitleSignal ? titlePipelineIntent : descriptionPipelineIntent;
        var codeIntent = hasTitleSignal ? titleCodeIntent : descriptionCodeIntent;

        var hasIntentSignal = infraIntent || monitoringIntent || pipelineIntent || codeIntent;

        var specializationScore = 0;
        if (infraIntent)
            specializationScore += ScoreCategory(agentProfile, toolNames, InfraKeywords, 26, 14);
        if (monitoringIntent)
            specializationScore += ScoreCategory(agentProfile, toolNames, MonitoringKeywords, 26, 14);
        if (pipelineIntent)
            specializationScore += ScoreCategory(agentProfile, toolNames, PipelineKeywords, 24, 12);
        if (codeIntent)
            specializationScore += ScoreCategory(agentProfile, toolNames, CodeKeywords, 24, 12);

        // If we can't infer intent confidently from task text, keep routing conservative and avoid aggressive reroutes.
        if (!hasIntentSignal)
            specializationScore += 2;

        var toolScore = toolNames.Length switch
        {
            0 => 0,
            <= 2 => 4,
            <= 5 => 8,
            _ => 12
        };

        var heuristicScore = 0;
        var username = NormalizeForCapability(worker.Info.Username);
        var isDevOps = username.Contains("devops", StringComparison.Ordinal);
        var isDeveloper = username.Contains("dev", StringComparison.Ordinal) && !isDevOps;

        if (infraIntent || monitoringIntent || pipelineIntent)
        {
            if (isDevOps) heuristicScore += 18;
            if (isDeveloper) heuristicScore -= 4;
        }

        if (codeIntent)
        {
            if (isDeveloper) heuristicScore += 16;
            if (isDevOps) heuristicScore -= 3;
        }

        if (string.Equals(worker.Info.Username, requestedWorkerUsername, StringComparison.OrdinalIgnoreCase))
            heuristicScore += 6;

        var hasDeclaredCapability = !string.IsNullOrWhiteSpace(worker.Info.Description) || toolNames.Length > 0;
        if (!hasDeclaredCapability)
            heuristicScore -= 40;

        var total = specializationScore + toolScore + heuristicScore;
        var intentSummary = $"infra={infraIntent},monitor={monitoringIntent},pipeline={pipelineIntent},code={codeIntent}";
        return new WorkerCapabilityScore(worker, total, specializationScore, toolScore, heuristicScore, hasDeclaredCapability, intentSummary);
    }

    private static int ScoreCategory(
        string agentProfile,
        IReadOnlyList<string> toolNames,
        IReadOnlyList<string> categoryKeywords,
        int specializationWeight,
        int toolWeight)
    {
        var specializationMatches = CountKeywordMatches(agentProfile, categoryKeywords);
        var toolMatch = toolNames.Any(t => HasAnyKeyword(t, categoryKeywords));

        var score = 0;
        if (specializationMatches > 0)
            score += specializationWeight + Math.Min(3, specializationMatches) * 4;
        else
            score -= 8;

        if (toolMatch)
            score += toolWeight;

        return score;
    }

    private static int CountKeywordMatches(string text, IReadOnlyList<string> keywords)
    {
        var count = 0;
        foreach (var keyword in keywords)
        {
            if (ContainsKeyword(text, keyword))
                count++;
        }

        return count;
    }

    private static bool HasAnyKeyword(string text, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (ContainsKeyword(text, keyword))
                return true;
        }

        return false;
    }

    private static bool ContainsKeyword(string text, string keyword)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(keyword))
            return false;

        var normalizedText = NormalizeForCapability(text);
        if (normalizedText.Length == 0)
            return false;

        var keywordRaw = keyword.Trim().ToLowerInvariant();
        var wildcard = keywordRaw.EndsWith("*", StringComparison.Ordinal);
        if (wildcard)
            keywordRaw = keywordRaw[..^1];

        var normalizedKeyword = NormalizeForCapability(keywordRaw);
        if (normalizedKeyword.Length == 0)
            return false;

        // Phrase matching can use direct contains on normalized text.
        if (normalizedKeyword.Contains(' ', StringComparison.Ordinal))
            return normalizedText.Contains(normalizedKeyword, StringComparison.Ordinal);

        var tokens = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (wildcard)
            return tokens.Any(t => t.StartsWith(normalizedKeyword, StringComparison.Ordinal));

        return tokens.Any(t => string.Equals(t, normalizedKeyword, StringComparison.Ordinal));
    }

    private static string NormalizeForCapability(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var lower = value.Trim().ToLowerInvariant();
        var compact = new char[lower.Length];
        var pos = 0;
        var previousWasSpace = false;
        foreach (var ch in lower)
        {
            if (char.IsLetterOrDigit(ch))
            {
                compact[pos++] = ch;
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                compact[pos++] = ' ';
                previousWasSpace = true;
            }
        }

        if (pos == 0)
            return string.Empty;

        return new string(compact, 0, pos).Trim();
    }

    private async Task TryMirrorWorkerProgressAsync(
        OrchestrationTask task,
        Agent workerAgent,
        string message,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            Log.Information("[ORCHESTRATION] Suppressed empty worker progress mirror for task {TaskId}", task.Id);
            return;
        }

        if (IsNearDuplicateText(task.LastPublicProgressSummary, message))
        {
            Log.Information("[ORCHESTRATION] Suppressed duplicate worker progress mirror for task {TaskId}", task.Id);
            return;
        }

        PublicWorkerEventType eventType;
        if (task.PublicStartedMessageAtUtc == null)
        {
            task.PublicStartedMessageAtUtc = DateTime.UtcNow;
            eventType = PublicWorkerEventType.Started;
        }
        else if (task.PublicProgressMessageAtUtc == null)
        {
            task.PublicProgressMessageAtUtc = DateTime.UtcNow;
            eventType = PublicWorkerEventType.Progress;
        }
        else
        {
            Log.Information("[ORCHESTRATION] Suppressed worker progress mirror for task {TaskId}: progress quota reached", task.Id);
            return;
        }

        task.LastPublicProgressSummary = message;
        await _dbContext.SaveChangesAsync(ct);

        await MirrorToChatAsync(task.ChannelId, workerAgent, message, ct);
        Log.Information("[ORCHESTRATION] Mirrored worker {EventType} update for task {TaskId} from {WorkerUsername}",
            eventType, task.Id, workerAgent.Info.Username);
    }

    private async Task TryMirrorWorkerFinalAsync(
        OrchestrationTask task,
        Agent workerAgent,
        string summary,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(summary))
        {
            Log.Information("[ORCHESTRATION] Suppressed empty worker final mirror for task {TaskId}", task.Id);
            return;
        }

        if (task.PublicFinalMessageAtUtc != null)
        {
            Log.Information("[ORCHESTRATION] Suppressed duplicate worker final mirror for task {TaskId}", task.Id);
            return;
        }

        task.PublicFinalMessageAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await MirrorToChatAsync(task.ChannelId, workerAgent, summary, ct);
        Log.Information("[ORCHESTRATION] Mirrored worker {EventType} update for task {TaskId} from {WorkerUsername}",
            PublicWorkerEventType.Final, task.Id, workerAgent.Info.Username);
    }

    private static bool IsNearDuplicateText(string? previous, string current)
    {
        if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrWhiteSpace(current))
            return false;

        var prev = NormalizeForDedup(previous);
        var next = NormalizeForDedup(current);

        if (string.IsNullOrEmpty(prev) || string.IsNullOrEmpty(next))
            return false;

        if (string.Equals(prev, next, StringComparison.Ordinal))
            return true;

        var minLength = Math.Min(prev.Length, next.Length);
        var maxLength = Math.Max(prev.Length, next.Length);
        if (minLength < 25 || maxLength == 0)
            return false;

        var lengthRatio = (double)minLength / maxLength;
        if (lengthRatio < 0.85d)
            return false;

        return prev.Contains(next, StringComparison.Ordinal) || next.Contains(prev, StringComparison.Ordinal);
    }

    private static string BuildIntentSignature(string text)
    {
        var normalizedTask = NormalizeForCapability(text);
        if (normalizedTask.Length == 0)
            return "generic";

        var flags = new List<string>(4);
        if (HasAnyKeyword(normalizedTask, InfraKeywords))
            flags.Add("infra");
        if (HasAnyKeyword(normalizedTask, MonitoringKeywords))
            flags.Add("monitor");
        if (HasAnyKeyword(normalizedTask, PipelineKeywords))
            flags.Add("pipeline");
        if (HasAnyKeyword(normalizedTask, CodeKeywords))
            flags.Add("code");

        return flags.Count == 0 ? "generic" : string.Join('|', flags);
    }

    private static string NormalizeForDedup(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
            return string.Empty;

        var compact = new char[normalized.Length];
        var position = 0;
        var previousWasSpace = false;
        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                compact[position++] = ch;
                previousWasSpace = false;
                continue;
            }

            if (char.IsWhiteSpace(ch) && !previousWasSpace)
            {
                compact[position++] = ' ';
                previousWasSpace = true;
            }
        }

        if (position == 0)
            return string.Empty;

        return new string(compact, 0, position).Trim();
    }

    private static string TruncateForLog(string value)
    {
        const int maxLength = 180;
        if (string.IsNullOrEmpty(value))
            return value;

        return value.Length <= maxLength ? value : value[..maxLength] + "…";
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
