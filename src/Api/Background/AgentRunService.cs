using System.Runtime.CompilerServices;
using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.AgentServices;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Domain.Tools.BackEnd.Orchestration;
using AzureOpsCrew.Domain.Orchestration;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Serilog;
using AzureOpsCrew.Domain.Tools.FrontEnd;
using AzureOpsCrew.Domain.Tools.Mcp;
using AzureOpsCrew.Api.Background.ToolExecutors;
using AzureOpsCrew.Infrastructure.Ai.AgentServices;
using System.Text.Json;

namespace AzureOpsCrew.Api.Background;

public class AgentRunService
{
    private readonly Guid _runId = Guid.NewGuid();
    private readonly AzureOpsCrewContext _dbContext;
    private readonly IProviderFacadeResolver _providerFactory;
    private readonly ToolCallRouter _toolCallRouter;
    private readonly IAiAgentFactory _aiAgentFactory;
    private readonly ContextService _contextService;
    private readonly IChannelEventBroadcaster? _channelEventBroadcaster;

    public AgentRunService(IServiceProvider serviceProvider)
    {
        _dbContext = serviceProvider.GetRequiredService<AzureOpsCrewContext>();
        _providerFactory = serviceProvider.GetRequiredService<IProviderFacadeResolver>();
        _toolCallRouter = serviceProvider.GetRequiredService<ToolCallRouter>();
        _aiAgentFactory = serviceProvider.GetRequiredService<IAiAgentFactory>();
        _contextService = serviceProvider.GetRequiredService<ContextService>();
        // Event broadcaster is optional - used for both channels and DMs
        _channelEventBroadcaster = serviceProvider.GetService<IChannelEventBroadcaster>();
    }

    /// <summary>
    /// Orchestration-aware entry point. Accepts a trigger with kind/taskId context.
    /// </summary>
    public Task Run(AgentTrigger trigger, CancellationToken ct)
        => Run(trigger.AgentId, trigger.ChatId, ct, trigger);

    public Task Run(Guid agentId, Guid chatId, CancellationToken ct)
        => Run(agentId, chatId, ct, trigger: null);

    private async Task Run(Guid agentId, Guid chatId, CancellationToken ct, AgentTrigger? trigger)
    {
        Log.Information("[BACKGROUND] Starting agent run: {AgentId}, chat: {ChatId}, trigger: {TriggerKind}, taskId: {TaskId}",
            agentId, chatId, trigger?.Kind.ToString() ?? "legacy", trigger?.TaskId);

        // Pre-load data to determine chat type for status broadcasts
        var initialData = await LoadAgentRunData(agentId, chatId, ct, trigger);
        if (!IsRunAllowed(initialData, trigger, out var rejectReason))
        {
            Log.Information("[BACKGROUND] Skipping agent run: {AgentId}, chat: {ChatId}, trigger: {TriggerKind}. Reason: {Reason}",
                agentId, chatId, trigger?.Kind.ToString() ?? "legacy", rejectReason);
            return;
        }

        try
        {
            // Broadcast "Running" status at the start
            await BroadcastAgentStatus(initialData, "Running");

            var iteration = 0;
            const int maxIterations = 50;
            var delegatedAssignments = new List<(string Agent, string Title)>();
            var delegatedAssignmentKeys = new HashSet<string>(StringComparer.Ordinal);
            // multiple iterations for one run, stops when outputted a final text content
            while (!ct.IsCancellationRequested && iteration < maxIterations)
            {
                iteration++;
                var chatMessageId = Guid.NewGuid(); // This will be used to link all thoughts from this iteration to the same message
                Log.Debug("[BACKGROUND] Generated new ChatMessageId {ChatMessageId} for agent {AgentId} iteration {Iteration}", chatMessageId, agentId, iteration);

                // load new DB state for each iteration to get the latest messages and thoughts
                var data = await LoadAgentRunData(agentId, chatId, ct, trigger);
                if (!IsRunAllowed(data, trigger, out var stopReason))
                {
                    Log.Information("[BACKGROUND] Stopping agent run loop: {AgentId}, chat: {ChatId}, trigger: {TriggerKind}. Reason: {Reason}",
                        agentId, chatId, trigger?.Kind.ToString() ?? "legacy", stopReason);
                    break;
                }

                // Make one LLM call
                var newAgentThoughts = new List<AocAgentThought>();
                await foreach (var agentThought in CallLlm(data, chatMessageId, ct))
                {
                    newAgentThoughts.Add(agentThought);
                }

                // Compact text content and save to DB
                await SaveRawLlmHttpCall(agentId, chatId, newAgentThoughts, ct);
                AgentThoughtHelper.SquashTextContent(newAgentThoughts);
                await SaveAgentThoughts(agentId, chatId, newAgentThoughts, ct);

                // Broadcast reasoning content via SignalR
                if (data.DmChannel != null && _channelEventBroadcaster != null)
                {
                    foreach (var thought in newAgentThoughts)
                    {
                        if (thought.ContentDto.ToAocAiContent() is AocTextReasoningContent reasoning)
                        {
                            var evt = new ReasoningContentEvent
                            {
                                Text = reasoning.Text,
                                Timestamp = DateTimeOffset.UtcNow,
                            };
                            await _channelEventBroadcaster.BroadcastDmReasoningContentAsync(data.DmChannel.Id, evt);
                        }
                    }
                }

                // Execute tools and save results to DB
                var newToolCallResults = new List<AocAgentThought>();
                var newToolCalls = newAgentThoughts
                    .Select(m => m.ContentDto.ToAocAiContent())
                    .OfType<AocFunctionCallContent>()
                    .ToList();

                // ToDo: Add support for parallel tool calls if needed. For now we execute them sequentially for simplicity.
                foreach (var toolCall in newToolCalls)
                {
                    // Broadcast tool call start event before execution
                    if (data.DmChannel != null && _channelEventBroadcaster != null)
                    {
                        var startEvt = new ToolCallStartEvent
                        {
                            ToolName = toolCall.Name,
                            CallId = toolCall.CallId,
                            Args = toolCall.Arguments ?? new Dictionary<string, object?>(),
                            Timestamp = DateTimeOffset.UtcNow,
                        };
                        await _channelEventBroadcaster.BroadcastDmToolCallStartAsync(data.DmChannel.Id, startEvt);
                    }

                    var toolCallResult = await _toolCallRouter.ExecuteToolCall(toolCall, data, ct);
                    var toolResultMessage = AocAgentThought.FromContent(toolCallResult, ChatRole.Tool, data.Agent.Info.Username, DateTime.UtcNow, Guid.NewGuid());
                    newToolCallResults.Add(toolResultMessage);

                    if (data.DmChannel != null && _channelEventBroadcaster != null)
                    {
                        var isError = (toolCallResult.Result as ToolCallResult)?.IsError ?? false;
                        var evt = new ToolCallCompletedEvent
                        {
                            ToolName = toolCall.Name,
                            CallId = toolCall.CallId,
                            Args = toolCall.Arguments ?? new Dictionary<string, object?>(),
                            Result = toolCallResult.Result,
                            IsError = isError,
                            Timestamp = DateTimeOffset.UtcNow,
                        };
                        await _channelEventBroadcaster.BroadcastDmToolCallCompletedAsync(data.DmChannel.Id, evt);
                    }
                }
                await SaveAgentThoughts(agentId, chatId, newToolCallResults, ct);
                CaptureDelegatedAssignments(newToolCalls, delegatedAssignments, delegatedAssignmentKeys);

                // If the agent called the skipTurn tool, we consider that it has finished its run and we stop the loop.
                // This allows agents to explicitly signal that they want to skip their turn and let other agents or the human take the lead.
                if (newToolCalls.Any(c => string.Equals(c.Name, SkipTurnTool.ToolName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Log.Information("[BACKGROUND] Agent {AgentId} decided to skip its turn in chat {ChatId}", agentId, chatId);
                    break;
                }

                // In orchestrated channels, suppress worker text output — workers communicate via orchestration tools only.
                var isOrchestratedWorker = data.Channel?.IsOrchestrated == true
                    && data.Channel.ManagerAgentId != data.Agent.Id;
                var isOrchestratedManager = data.Channel?.IsOrchestrated == true
                    && data.Channel.ManagerAgentId == data.Agent.Id;

                var lastTextAgentThought = newAgentThoughts.LastOrDefault(t => t.ContentDto.ToAocAiContent() is AocTextContent);
                var lastTextContent = lastTextAgentThought?.ContentDto.ToAocAiContent() as AocTextContent;
                if (lastTextContent != null && !string.IsNullOrEmpty(lastTextContent.Text))
                {
                    if (isOrchestratedWorker)
                    {
                        Log.Debug("[BACKGROUND] Suppressing worker text output in orchestrated channel for agent {AgentId}", agentId);
                    }
                    else if (isOrchestratedManager && IsDuplicateManagerPublicUpdate(data, lastTextContent.Text))
                    {
                        Log.Information("[BACKGROUND] Suppressed duplicate manager public update for agent {AgentId} in channel {ChannelId}",
                            data.Agent.Id, data.Channel!.Id);
                    }
                    else
                    {
                        var publicText = isOrchestratedManager
                            ? BuildManagerPublicMessage(newToolCalls, delegatedAssignments, lastTextContent.Text)
                            : lastTextContent.Text;

                        if (string.IsNullOrWhiteSpace(publicText))
                        {
                            Log.Information("[BACKGROUND] Suppressed empty manager public update for agent {AgentId} in channel {ChannelId}",
                                data.Agent.Id, data.Channel!.Id);
                        }
                        else if (isOrchestratedManager && IsDuplicateManagerPublicUpdate(data, publicText))
                        {
                            Log.Information("[BACKGROUND] Suppressed duplicate manager public update after sanitization for agent {AgentId} in channel {ChannelId}",
                                data.Agent.Id, data.Channel!.Id);
                        }
                        else
                        {
                            var message = await SaveLastMessage(data, publicText, lastTextAgentThought!.Id, ct);
                            await Broadcast(data, message);
                        }
                    }
                }

                // If there are no tool calls, we can assume the agent has finished its run after one iteration.
                // This is a simplification and can be improved by adding explicit signals in the future.
                if (newToolCalls.Count == 0)
                {
                    break;
                }

                // In orchestrated channels, break after a worker calls completeTask or failTask
                if (isOrchestratedWorker)
                {
                    var calledTerminalTool = newToolCalls.Any(c =>
                        string.Equals(c.Name, "completeTask", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Name, "failTask", StringComparison.OrdinalIgnoreCase));
                    if (calledTerminalTool)
                    {
                        Log.Information("[BACKGROUND] Orchestrated worker {AgentId} completed/failed task — stopping loop", agentId);
                        break;
                    }
                }
            }

            if (iteration >= maxIterations)
            {
                Log.Warning("[BACKGROUND] Agent run hit max iterations: {AgentId}, chat: {ChatId}", agentId, chatId);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[BACKGROUND] Agent run failed: {AgentId}, chat: {ChatId}", agentId, chatId);
            var errorMsg = ex.Message.Length > 300 ? ex.Message[..300] + "…" : ex.Message;
            await BroadcastAgentStatus(initialData, "Error", errorMsg);
            throw;
        }

        await BroadcastAgentStatus(initialData, "Idle");
        Log.Information("[BACKGROUND] Agent run completed: {AgentId}, chat: {ChatId}", agentId, chatId);
    }

    private async Task Broadcast(AgentRunData data, Message message)
    {
        // Broadcast the message
        if (data.Channel != null && _channelEventBroadcaster != null)
        {
            await _channelEventBroadcaster.BroadcastMessageAddedAsync(data.Channel.Id, message);
        }
        else if (data.DmChannel != null && _channelEventBroadcaster != null)
        {
            await _channelEventBroadcaster.BroadcastDmMessageAddedAsync(data.DmChannel.Id, message);
        }
    }

    private async Task BroadcastAgentStatus(AgentRunData data, string status, string? errorMessage = null)
    {
        if (data.DmChannel != null && _channelEventBroadcaster != null)
        {
            var evt = new AgentStatusEvent
            {
                AgentId = data.Agent.Id,
                Status = status,
                ErrorMessage = errorMessage,
                Timestamp = DateTimeOffset.UtcNow,
            };
            await _channelEventBroadcaster.BroadcastDmAgentStatusAsync(data.DmChannel.Id, evt);
        }
    }

    private static bool IsRunAllowed(AgentRunData data, AgentTrigger? trigger, out string reason)
    {
        reason = string.Empty;

        var channel = data.Channel;
        if (channel == null || !channel.IsOrchestrated)
        {
            return true;
        }

        if (trigger is null)
        {
            reason = "missing trigger for orchestrated channel";
            return false;
        }

        var isManager = channel.ManagerAgentId == data.Agent.Id;
        if (isManager)
        {
            if (trigger.Kind is AgentTriggerKind.UserMessage or AgentTriggerKind.TaskUpdated)
            {
                return true;
            }

            reason = $"manager cannot run on trigger kind '{trigger.Kind}'";
            return false;
        }

        if (trigger.Kind != AgentTriggerKind.TaskAssigned)
        {
            reason = $"worker can run only on TaskAssigned, actual '{trigger.Kind}'";
            return false;
        }

        if (trigger.TaskId is null)
        {
            reason = "worker task trigger does not contain taskId";
            return false;
        }

        var task = data.CurrentTask;
        if (task is null)
        {
            reason = $"task '{trigger.TaskId}' not found";
            return false;
        }

        if (task.ChannelId != channel.Id)
        {
            reason = $"task '{task.Id}' belongs to another channel";
            return false;
        }

        if (task.AssignedAgentId != data.Agent.Id)
        {
            reason = $"task '{task.Id}' is assigned to another agent";
            return false;
        }

        if (task.Status is OrchestrationTaskStatus.Completed or OrchestrationTaskStatus.Failed)
        {
            reason = $"task '{task.Id}' is already terminal ({task.Status})";
            return false;
        }

        return true;
    }

    private static bool IsDuplicateManagerPublicUpdate(AgentRunData data, string candidateText)
    {
        if (data.Channel?.ManagerAgentId is null || string.IsNullOrWhiteSpace(candidateText))
            return false;

        var thresholdUtc = DateTime.UtcNow.AddMinutes(-10);
        var recentManagerMessages = data.ChatMessages
            .Where(m =>
                m.ChannelId == data.Channel.Id &&
                m.AgentId == data.Channel.ManagerAgentId &&
                m.PostedAt >= thresholdUtc)
            .OrderByDescending(m => m.PostedAt)
            .Take(3)
            .ToList();

        return recentManagerMessages.Any(m => IsNearDuplicateText(m.Text, candidateText));
    }

    private static string BuildManagerPublicMessage(
        IReadOnlyList<AocFunctionCallContent> toolCalls,
        IReadOnlyList<(string Agent, string Title)> delegatedAssignments,
        string modelText)
    {
        if (delegatedAssignments.Count > 0)
        {
            return BuildDelegationSummary(delegatedAssignments);
        }

        var createTaskCalls = toolCalls
            .Where(c => string.Equals(c.Name, OrchestrationTools.CreateTaskName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (createTaskCalls.Count == 0)
        {
            var textWithoutUnverifiedDelegation = StripUnverifiedDelegationClaims(modelText);
            return ClampAndCompactManagerText(textWithoutUnverifiedDelegation, maxLength: 700);
        }

        var uniqueAssignments = new List<(string Agent, string Title)>();
        var assignmentKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var toolCall in createTaskCalls)
        {
            var agentUsername = GetStringArg(toolCall.Arguments, "agentUsername") ?? "worker";
            var title = GetStringArg(toolCall.Arguments, "title") ?? "task";
            var dedupKey = $"{NormalizeForDedup(agentUsername)}|{NormalizeForDedup(title)}";

            if (!assignmentKeys.Add(dedupKey))
                continue;

            uniqueAssignments.Add((agentUsername, title));
        }

        if (uniqueAssignments.Count == 0)
        {
            return "Delegated tasks to specialists. Waiting for worker results.";
        }

        return BuildDelegationSummary(uniqueAssignments);
    }

    private static string BuildDelegationSummary(IReadOnlyList<(string Agent, string Title)> assignments)
    {
        const int maxVisibleAssignments = 6;
        var lines = assignments
            .Take(maxVisibleAssignments)
            .Select(a => $"- {a.Agent}: {a.Title}")
            .ToList();

        var hiddenCount = assignments.Count - maxVisibleAssignments;
        if (hiddenCount > 0)
        {
            lines.Add($"- ... and {hiddenCount} additional delegated tasks");
        }

        var summary = "Delegated tasks:\n" + string.Join('\n', lines) + "\nWaiting for worker results.";
        return ClampAndCompactManagerText(summary, maxLength: 700);
    }

    private static void CaptureDelegatedAssignments(
        IReadOnlyList<AocFunctionCallContent> toolCalls,
        List<(string Agent, string Title)> delegatedAssignments,
        HashSet<string> delegatedAssignmentKeys)
    {
        foreach (var toolCall in toolCalls)
        {
            if (!string.Equals(toolCall.Name, OrchestrationTools.CreateTaskName, StringComparison.OrdinalIgnoreCase))
                continue;

            var agentUsername = GetStringArg(toolCall.Arguments, "agentUsername") ?? "worker";
            var title = GetStringArg(toolCall.Arguments, "title") ?? "task";
            var dedupKey = $"{NormalizeForDedup(agentUsername)}|{NormalizeForDedup(title)}";
            if (!delegatedAssignmentKeys.Add(dedupKey))
                continue;

            delegatedAssignments.Add((agentUsername, title));
        }
    }

    private static string ClampAndCompactManagerText(string text, int maxLength)
    {
        var compact = CompactDuplicateLines(text);

        if (compact.Length <= maxLength)
            return compact;

        return compact[..maxLength] + "\n[truncated]";
    }

    private static string CompactDuplicateLines(string text)
    {
        var normalizedText = text.Replace("\r\n", "\n");
        var lines = normalizedText.Split('\n');

        var result = new List<string>(Math.Min(lines.Length, 24));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var suppressed = 0;
        var appendedNonEmpty = false;

        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.Length == 0)
            {
                if (appendedNonEmpty && result.Count > 0 && result[^1].Length > 0)
                {
                    result.Add(string.Empty);
                }
                continue;
            }

            var dedupKey = NormalizeForDedup(trimmed);
            if (dedupKey.Length == 0)
                continue;

            if (!seen.Add(dedupKey))
            {
                suppressed++;
                continue;
            }

            result.Add(trimmed);
            appendedNonEmpty = true;

            if (result.Count >= 24)
            {
                suppressed += lines.Length - index - 1;
                break;
            }
        }

        if (suppressed > 0)
        {
            result.Add($"[suppressed {suppressed} repeated lines]");
        }

        return string.Join('\n', result).Trim();
    }

    private static string StripUnverifiedDelegationClaims(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var filtered = new List<string>(lines.Length);
        var removedAny = false;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                filtered.Add(raw);
                continue;
            }

            if (LooksLikeDelegationClaim(trimmed))
            {
                removedAny = true;
                continue;
            }

            filtered.Add(raw);
        }

        if (!removedAny)
            return text;

        var rebuilt = string.Join('\n', filtered).Trim();
        if (rebuilt.Length > 0)
            return rebuilt;

        return "Acknowledged. I will coordinate the next steps and provide a concise update.";
    }

    private static bool LooksLikeDelegationClaim(string line)
    {
        var normalized = NormalizeForDedup(line);
        if (normalized.Length == 0)
            return false;

        return normalized.Contains("assigning task", StringComparison.Ordinal)
            || normalized.Contains("assigning tasks", StringComparison.Ordinal)
            || normalized.Contains("назначаю задачу", StringComparison.Ordinal)
            || normalized.Contains("назначаю задачи", StringComparison.Ordinal)
            || normalized.Contains("назначил задачу", StringComparison.Ordinal)
            || normalized.Contains("получил задачу", StringComparison.Ordinal)
            || normalized.Contains("делегирую задачу", StringComparison.Ordinal)
            || normalized.Contains("поручаю задачу", StringComparison.Ordinal);
    }

    private static bool IsNearDuplicateText(string previous, string current)
    {
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

    private static string? GetStringArg(IDictionary<string, object?>? args, string key)
    {
        if (args is null || !args.TryGetValue(key, out var value) || value is null)
            return null;

        if (value is JsonElement element)
        {
            return element.ValueKind == JsonValueKind.String
                ? element.GetString()
                : element.GetRawText();
        }

        return value.ToString();
    }

    private async Task<AgentRunData> LoadAgentRunData(Guid agentId, Guid chatId, CancellationToken ct, AgentTrigger? trigger = null)
    {
        Log.Debug("[BACKGROUND] Loading data for agent {AgentId}, chat {ChatId}", agentId, chatId);

        // load agent
        var agent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null)
        {
            Log.Error("[BACKGROUND] Agent {AgentId} not found", agentId);
            throw new InvalidOperationException($"Agent with id {agentId} not found.");
        }

        // load provider
        var provider = await _dbContext.Providers.FirstOrDefaultAsync(p => p.Id == agent.ProviderId, ct);
        if (provider is null)
        {
            Log.Error("[BACKGROUND] Provider {ProviderId} not found for agent {AgentId}", agent.ProviderId, agentId);
            throw new InvalidOperationException($"Provider with id {agent.ProviderId} not found.");
        }

        // determine chat type & load chat
        var channel = await _dbContext.Channels.FirstOrDefaultAsync(c => c.Id == chatId, ct);
        var dm = await _dbContext.Dms.FirstOrDefaultAsync(d => d.Id == chatId, ct);
        var isChannel = channel != null;
        var isDm = dm != null;
        if (!isChannel && !isDm)
        {
            Log.Error("[BACKGROUND] Chat {ChatId} not found", chatId);
            throw new InvalidOperationException($"Chat with id {chatId} not found.");
        }

        // load all messages in the chat for context
        var chatMessages = isChannel
            ? await _dbContext.Messages.Where(x => x.ChannelId == chatId).OrderBy(m => m.PostedAt).ToListAsync(ct)
            : await _dbContext.Messages.Where(x => x.DmId == chatId).OrderBy(m => m.PostedAt).ToListAsync(ct);

        // load llm thoughts for the agent
        var llmThoughts = await _dbContext.AgentThoughts
            .Where(t => t.AgentId == agentId && t.ThreadId == chatId)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(ct);

        // load tools
        var backendTools = BackEndTools.All.Select(t => t.GetDeclaration()).ToList();
        if (channel?.IsOrchestrated == true && channel.ManagerAgentId != agentId)
        {
            // Workers in orchestrated mode should not rely on turn-taking or per-agent todo planning.
            var blockedToolNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                SkipTurnTool.ToolName,
                "wait",
                "createTodoItem",
                "listTodoItems",
                "markTodoItemCompleted",
                "deleteTodoItem",
            };
            backendTools = backendTools
                .Where(t => !blockedToolNames.Contains(t.Name))
                .ToList();
        }

        var frontEndTools = FrontEndTools.GetDeclarations();
        var tools = backendTools
            .Concat(frontEndTools)
            .ToList();

        // load MCP server configurations filtered by agent's bindings
        var agentMcpBindings = agent.Info.AvailableMcpServerTools;
        var agentMcpServerIds = agentMcpBindings.Select(b => b.McpServerConfigurationId).ToHashSet();

        var mcpServers = await _dbContext.McpServerConfigurations
            .Where(s => s.IsEnabled && agentMcpServerIds.Contains(s.Id))
            .ToListAsync(ct);

        var mcpToolDeclarations = McpToolDeclarationBuilder.Build(mcpServers, agentMcpBindings);
        tools.AddRange(mcpToolDeclarations);

        // ── Orchestration tool injection ──
        OrchestrationTask? currentTask = null;
        if (channel != null && channel.IsOrchestrated)
        {
            var isManager = channel.ManagerAgentId == agentId;
            if (isManager)
            {
                // Manager gets createTask + listTasks
                tools.AddRange(OrchestrationTools.GetManagerTools());
            }
            else if (trigger?.Kind == AgentTriggerKind.TaskAssigned)
            {
                // Worker on task assignment gets postTaskProgress + completeTask + failTask
                tools.AddRange(OrchestrationTools.GetWorkerTools());
            }

            // Load current task if trigger references one
            if (trigger?.TaskId != null)
            {
                currentTask = await _dbContext.OrchestrationTasks
                    .FirstOrDefaultAsync(t => t.Id == trigger.TaskId, ct);
            }
        }

        // load participant agents in the chat for context
        var agentIds = isChannel
            ? channel?.AgentIds ?? [] // todo: concat user ids when we have users in channels
            : new List<Guid?> { dm!.Agent1Id, dm!.Agent2Id }.Where(id => id != null).Select(x => x!.Value).ToArray();
        var participantAgents = await _dbContext.Agents.Where(a => agentIds.Contains(a.Id)).ToListAsync(ct);

        Log.Debug("[BACKGROUND] Loaded data for agent {AgentId}: {MessageCount} messages, {ThoughtCount} thoughts, {ToolCount} tools",
            agentId, chatMessages.Count, llmThoughts.Count, tools.Count);

        return new AgentRunData
        {
            Agent = agent,
            Provider = provider,
            Channel = channel,
            DmChannel = dm,
            ChatMessages = chatMessages,
            LlmThoughts = llmThoughts,
            Tools = tools,
            McpServers = mcpServers,
            ParticipantAgents = participantAgents,
            Trigger = trigger,
            CurrentTask = currentTask,
        };
    }

    private async IAsyncEnumerable<AocAgentThought> CallLlm(AgentRunData data, Guid chatMessageId, [EnumeratorCancellation] CancellationToken ct)
    {
        Log.Debug("[BACKGROUND] Calling LLM for agent {AgentId} with model {Model}", data.Agent.Id, data.Agent.Info.Model);

        var providerFacade = _providerFactory.GetService(data.Provider.ProviderType);
        var chatClient = providerFacade.CreateChatClient(data.Provider, data.Agent.Info.Model, CancellationToken.None);
        var fClient = new FunctionInvokingChatClient(chatClient);

        var aiAgent = _aiAgentFactory.Create(fClient, data);
        var allMessages = _contextService.PrepareContext(data);

        var agentSession = await aiAgent.CreateSessionAsync(ct);
        var runOptions = new AgentRunOptions { AllowBackgroundResponses = false };

        await foreach (AgentResponseUpdate update in aiAgent.RunStreamingAsync(allMessages, agentSession, runOptions, ct))
        {
            var contents = update.Contents;
            foreach (var content in contents)
            {
                var parsedContent = AocAiContent.FromAiContent(content);
                if (parsedContent != null)
                {
                    // Ensure that messages are created in chronological order.
                    // This is important for the agent to process messages in the correct order, especially when there are tool calls involved.
                    await Task.Delay(TimeSpan.FromMilliseconds(1));
                    var now = DateTime.UtcNow;

                    var newMessage = AocAgentThought.FromContent(parsedContent, update.Role ?? ChatRole.Assistant, data.Agent.Info.Username, now, chatMessageId);
                    Log.Verbose("[BACKGROUND] LLM response: {Role} - {ContentType}", update.Role ?? ChatRole.Assistant, parsedContent.GetType().Name);

                    yield return newMessage;
                }
            }
        }
    }

    private async Task SaveRawLlmHttpCall(Guid agentId, Guid chatId, List<AocAgentThought> newMessages, CancellationToken ct)
    {
        // Separate out messages related to HTTP client calls
        const string httpClientRole = "HTTP_CLIENT";
        var httpClientMessages = newMessages.Where(m => m.Role.Value == httpClientRole).ToArray();
        newMessages.RemoveAll(x => x.Role.Value == httpClientRole);

        // We should have two messages for each http client call: one for the request and one for the response
        var httpRequestMessage = httpClientMessages.Length > 0 ? httpClientMessages[0] : null;
        var httpResponseMessage = httpClientMessages.Length > 1 ? httpClientMessages[1] : null;

        // Insert them in this activity to not pass huge strings between activities (they are stored in Temporal)
        var rawCall = new RawLlmHttpCall
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            ThreadId = chatId,
            RunId = _runId,
            HttpRequest = (httpRequestMessage?.ContentDto?.ToAocAiContent() as AocTextContent)?.Text ?? "<empty>",
            HttpResponse = (httpResponseMessage?.ContentDto?.ToAocAiContent() as AocTextContent)?.Text ?? "<empty>",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.RawLlmHttpCalls.Add(rawCall);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task SaveAgentThoughts(Guid agentId, Guid chatId, List<AocAgentThought> newAgentThoughts, CancellationToken ct)
    {
        var newDomainThoughts = newAgentThoughts.Select(t => t.ToDomain(agentId, chatId, _runId)).ToList();
        _dbContext.AgentThoughts.AddRange(newDomainThoughts);
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<Message> SaveLastMessage(AgentRunData data, string text, Guid agentThoughtId, CancellationToken ct)
    {
        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = text,
            PostedAt = DateTime.UtcNow,
            AgentId = data.Agent.Id,
            AuthorName = data.Agent.Info.Username,
            AgentThoughtId = agentThoughtId,
        };

        if (data.Channel != null)
        {
            message.ChannelId = data.Channel.Id;
        }
        else
        {
            message.DmId = data.DmChannel!.Id;
        }
        _dbContext.Messages.Add(message);
        await _dbContext.SaveChangesAsync(ct);
        Log.Debug("[BACKGROUND] Saved message for agent {AgentId} to {ChatType}", data.Agent.Id, data.Channel != null ? "channel" : "DM");

        return message;
    }

}
