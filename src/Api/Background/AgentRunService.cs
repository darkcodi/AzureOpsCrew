using System.Runtime.CompilerServices;
using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.AgentServices;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
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

namespace AzureOpsCrew.Api.Background;

public class AgentRunService
{
    private readonly Guid _runId = Guid.NewGuid();
    private readonly AzureOpsCrewContext _dbContext;
    private readonly IProviderFacadeResolver _providerFactory;
    private readonly ToolCallRouter _toolCallRouter;
    private readonly BackendToolExecutor _backendToolExecutor;
    private readonly IAiAgentFactory _aiAgentFactory;
    private readonly ContextService _contextService;
    private readonly IChannelEventBroadcaster? _channelEventBroadcaster;

    public AgentRunService(IServiceProvider serviceProvider)
    {
        _dbContext = serviceProvider.GetRequiredService<AzureOpsCrewContext>();
        _providerFactory = serviceProvider.GetRequiredService<IProviderFacadeResolver>();
        _toolCallRouter = serviceProvider.GetRequiredService<ToolCallRouter>();
        _backendToolExecutor = serviceProvider.GetRequiredService<BackendToolExecutor>();
        _aiAgentFactory = serviceProvider.GetRequiredService<IAiAgentFactory>();
        _contextService = serviceProvider.GetRequiredService<ContextService>();
        // Event broadcaster is optional - used for both channels and DMs
        _channelEventBroadcaster = serviceProvider.GetService<IChannelEventBroadcaster>();
    }

    public async Task Run(Guid agentId, Guid chatId, CancellationToken ct)
    {
        Log.Information("[BACKGROUND] Starting agent run: {AgentId}, chat: {ChatId}", agentId, chatId);

        // Pre-load data to determine chat type for status broadcasts
        var initialData = await LoadAgentRunData(agentId, chatId, ct);
        var waitingForApproval = false;

        try
        {
            // Broadcast "Running" status at the start
            await BroadcastAgentStatus(initialData, "Running");

            var iteration = 0;
            const int maxIterations = 300;
            // multiple iterations for one run, stops when outputted a final text content
            while (!ct.IsCancellationRequested && iteration < maxIterations)
            {
                iteration++;
                var chatMessageId = Guid.NewGuid(); // This will be used to link all thoughts from this iteration to the same message
                Log.Debug("[BACKGROUND] Generated new ChatMessageId {ChatMessageId} for agent {AgentId} iteration {Iteration}", chatMessageId, agentId, iteration);

                // load new DB state for each iteration to get the latest messages and thoughts
                var data = await LoadAgentRunData(agentId, chatId, ct);

                // Check for pending approval resolutions before calling LLM
                var approvalResolution = await CheckAndResolveApprovals(agentId, chatId, data, ct);
                if (approvalResolution == ApprovalResolution.StillPending)
                {
                    Log.Information("[BACKGROUND] Agent {AgentId} still waiting for approval in chat {ChatId}", agentId, chatId);
                    waitingForApproval = true;
                    break;
                }
                if (approvalResolution == ApprovalResolution.Resolved)
                {
                    // Approval was resolved (approved -> executed, or rejected -> error saved)
                    // Continue to next iteration so LLM sees the result in context
                    continue;
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
                if (_channelEventBroadcaster != null)
                {
                    foreach (var thought in newAgentThoughts)
                    {
                        if (thought.ContentDto.ToAocAiContent() is AocTextReasoningContent reasoning)
                        {
                            var evt = new ReasoningContentEvent
                            {
                                Text = reasoning.Text,
                                AgentName = data.Agent.Info.Username,
                                Timestamp = DateTimeOffset.UtcNow,
                            };
                            if (data.Channel != null)
                            {
                                await _channelEventBroadcaster.BroadcastChannelReasoningContentAsync(data.Channel.Id, evt);
                            }
                            else if (data.DmChannel != null)
                            {
                                await _channelEventBroadcaster.BroadcastDmReasoningContentAsync(data.DmChannel.Id, evt);
                            }
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
                    // Check if this MCP tool requires approval before execution
                    var toolDeclaration = data.Tools.FirstOrDefault(t =>
                        string.Equals(t.Name, toolCall.Name, StringComparison.OrdinalIgnoreCase));

                    if (toolDeclaration?.ToolType == ToolType.McpServer)
                    {
                        // Save the approval request as an agent thought
                        var approvalRequest = new AocFunctionApprovalRequestContent
                        {
                            Id = Guid.NewGuid().ToString(),
                            FunctionCall = toolCall
                        };
                        var approvalThought = AocAgentThought.FromContent(
                            approvalRequest, ChatRole.Assistant, data.Agent.Info.Username,
                            DateTime.UtcNow, chatMessageId);
                        await SaveAgentThoughts(agentId, chatId, [approvalThought], ct);

                        // Broadcast approval request event
                        await BroadcastApprovalRequest(data, approvalRequest, toolCall);
                        await BroadcastAgentStatus(data, "WaitingForApproval");

                        Log.Information("[BACKGROUND] Agent {AgentId} requested approval for MCP tool {ToolName} in chat {ChatId}",
                            agentId, toolCall.Name, chatId);

                        waitingForApproval = true;
                        break; // break tool loop
                    }

                    // Broadcast tool call start event before execution
                    if (_channelEventBroadcaster != null && (data.Channel != null || data.DmChannel != null))
                    {
                        var startEvt = new ToolCallStartEvent
                        {
                            ToolName = toolCall.Name,
                            CallId = toolCall.CallId,
                            Args = toolCall.Arguments ?? new Dictionary<string, object?>(),
                            Timestamp = DateTimeOffset.UtcNow,
                        };
                        if (data.Channel != null)
                        {
                            await _channelEventBroadcaster.BroadcastChannelToolCallStartAsync(data.Channel.Id, startEvt);
                        }
                        else if (data.DmChannel != null)
                        {
                            await _channelEventBroadcaster.BroadcastDmToolCallStartAsync(data.DmChannel.Id, startEvt);
                        }
                    }

                    var toolCallResult = await _toolCallRouter.ExecuteToolCall(toolCall, data);
                    var toolResultMessage = AocAgentThought.FromContent(toolCallResult, ChatRole.Tool, data.Agent.Info.Username, DateTime.UtcNow, Guid.NewGuid());
                    newToolCallResults.Add(toolResultMessage);

                    if (_channelEventBroadcaster != null && (data.Channel != null || data.DmChannel != null))
                    {
                        var toolCallResultObj = toolCallResult.Result as ToolCallResult;
                        var isError = toolCallResultObj?.IsError ?? false;
                        var evt = new ToolCallCompletedEvent
                        {
                            ToolName = toolCall.Name,
                            CallId = toolCall.CallId,
                            Args = toolCall.Arguments ?? new Dictionary<string, object?>(),
                            Result = toolCallResultObj?.Result,
                            IsError = isError,
                            Timestamp = DateTimeOffset.UtcNow,
                        };
                        if (data.Channel != null)
                        {
                            await _channelEventBroadcaster.BroadcastChannelToolCallCompletedAsync(data.Channel.Id, evt);
                        }
                        else if (data.DmChannel != null)
                        {
                            await _channelEventBroadcaster.BroadcastDmToolCallCompletedAsync(data.DmChannel.Id, evt);
                        }
                    }
                }

                // Persist any tool results that were produced before we hit an approval gate.
                if (newToolCallResults.Count > 0)
                {
                    await SaveAgentThoughts(agentId, chatId, newToolCallResults, ct);
                }

                // If waiting for approval, break the iteration loop
                if (waitingForApproval)
                    break;

                // If the agent called the skipTurn tool, we consider that it has finished its run and we stop the loop.
                // This allows agents to explicitly signal that they want to skip their turn and let other agents or the human take the lead.
                if (newToolCalls.Any(c => string.Equals(c.Name, SkipTurnTool.ToolName, StringComparison.CurrentCultureIgnoreCase)))
                {
                    Log.Information("[BACKGROUND] Agent {AgentId} decided to skip its turn in chat {ChatId}", agentId, chatId);
                    break;
                }

                var lastTextAgentThought = newAgentThoughts.LastOrDefault(t => t.ContentDto.ToAocAiContent() is AocTextContent);
                var lastTextContent = lastTextAgentThought?.ContentDto.ToAocAiContent() as AocTextContent;
                if (lastTextContent != null)
                {
                    var message = await SaveLastMessage( data, lastTextContent.Text, lastTextAgentThought!.Id, ct);
                    await Broadcast(data, message);
                }

                // If there are no tool calls, we can assume the agent has finished its run after one iteration.
                // This is a simplification and can be improved by adding explicit signals in the future.
                if (newToolCalls.Count == 0)
                {
                    break;
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
            var errorMsg = ex.Message.Length > 300 ? ex.Message[..300] + "..." : ex.Message;
            await BroadcastAgentStatus(initialData, "Error", errorMsg);
            throw;
        }

        if (!waitingForApproval)
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
        if (_channelEventBroadcaster != null && (data.Channel != null || data.DmChannel != null))
        {
            var evt = new AgentStatusEvent
            {
                AgentId = data.Agent.Id,
                Status = status,
                ErrorMessage = errorMessage,
                Timestamp = DateTimeOffset.UtcNow,
            };
            if (data.Channel != null)
            {
                await _channelEventBroadcaster.BroadcastChannelAgentStatusAsync(data.Channel.Id, evt);
            }
            else if (data.DmChannel != null)
            {
                await _channelEventBroadcaster.BroadcastDmAgentStatusAsync(data.DmChannel.Id, evt);
            }
        }
    }

    private async Task<AgentRunData> LoadAgentRunData(Guid agentId, Guid chatId, CancellationToken ct)
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
        var backendTools = _backendToolExecutor.AllTools.Select(t => t.GetDeclaration()).ToList();
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

    private enum ApprovalResolution { None, StillPending, Resolved }

    private async Task<ApprovalResolution> CheckAndResolveApprovals(Guid agentId, Guid chatId, AgentRunData data, CancellationToken ct)
    {
        // Scan thoughts for approval requests and responses
        var thoughts = data.LlmThoughts
            .Select(t => AocAgentThought.FromDomain(t))
            .ToList();

        // Find the last approval request
        AocFunctionApprovalRequestContent? lastRequest = null;
        AocAgentThought? lastRequestThought = null;
        foreach (var thought in thoughts)
        {
            if (thought.ContentDto.ToAocAiContent() is AocFunctionApprovalRequestContent req)
            {
                lastRequest = req;
                lastRequestThought = thought;
            }
        }

        if (lastRequest?.FunctionCall == null)
            return ApprovalResolution.None;

        // Check if there's already a tool result for this call (already resolved)
        var callId = lastRequest.FunctionCall.CallId;
        var hasResult = thoughts.Any(t =>
            t.ContentDto.ToAocAiContent() is AocFunctionResultContent result
            && result.CallId == callId);

        if (hasResult)
            return ApprovalResolution.None; // Already resolved in a previous run

        // Check for a matching response
        AocFunctionApprovalResponseContent? response = null;
        foreach (var thought in thoughts)
        {
            if (thought.ContentDto.ToAocAiContent() is AocFunctionApprovalResponseContent resp
                && resp.Id == lastRequest.Id)
            {
                response = resp;
            }
        }

        if (response == null)
            return ApprovalResolution.StillPending;

        // Resolve the approval
        var toolCall = lastRequest.FunctionCall;
        var chatMessageId = lastRequestThought!.ChatMessageId;

        if (response.Approved)
        {
            Log.Information("[BACKGROUND] Approval granted for tool {ToolName} (CallId: {CallId}), executing",
                toolCall.Name, toolCall.CallId);

            // Broadcast tool call start
            await BroadcastToolCallStart(data, toolCall);

            // Execute the original tool call
            var toolCallResult = await _toolCallRouter.ExecuteToolCall(toolCall, data);
            var toolResultThought = AocAgentThought.FromContent(
                toolCallResult, ChatRole.Tool, data.Agent.Info.Username,
                DateTime.UtcNow, Guid.NewGuid());
            await SaveAgentThoughts(agentId, chatId, [toolResultThought], ct);

            // Broadcast tool call completed
            await BroadcastToolCallCompleted(data, toolCall, toolCallResult);
        }
        else
        {
            Log.Information("[BACKGROUND] Approval rejected for tool {ToolName} (CallId: {CallId})",
                toolCall.Name, toolCall.CallId);

            // Save synthetic error result
            var rejectionResult = new AocFunctionResultContent
            {
                CallId = toolCall.CallId,
                Result = new ToolCallResult(
                    CallId: toolCall.CallId,
                    Result: new { ErrorMessage = $"Tool execution rejected by user. Reason: {response.Reason ?? "No reason provided"}" },
                    IsError: true),
            };
            var rejectionThought = AocAgentThought.FromContent(
                rejectionResult, ChatRole.Tool, data.Agent.Info.Username,
                DateTime.UtcNow, Guid.NewGuid());
            await SaveAgentThoughts(agentId, chatId, [rejectionThought], ct);

            // Broadcast rejection as completed with error
            await BroadcastToolCallCompleted(data, toolCall, rejectionResult);
        }

        return ApprovalResolution.Resolved;
    }

    private async Task BroadcastApprovalRequest(AgentRunData data, AocFunctionApprovalRequestContent approvalRequest, AocFunctionCallContent toolCall)
    {
        if (_channelEventBroadcaster == null || (data.Channel == null && data.DmChannel == null))
            return;

        var evt = new ApprovalRequestEvent
        {
            ApprovalId = approvalRequest.Id,
            ToolName = toolCall.Name,
            CallId = toolCall.CallId,
            Args = toolCall.Arguments ?? new Dictionary<string, object?>(),
            AgentId = data.Agent.Id,
            AgentName = data.Agent.Info.Username,
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (data.Channel != null)
            await _channelEventBroadcaster.BroadcastChannelApprovalRequestAsync(data.Channel.Id, evt);
        else if (data.DmChannel != null)
            await _channelEventBroadcaster.BroadcastDmApprovalRequestAsync(data.DmChannel.Id, evt);
    }

    private async Task BroadcastToolCallStart(AgentRunData data, AocFunctionCallContent toolCall)
    {
        if (_channelEventBroadcaster == null || (data.Channel == null && data.DmChannel == null))
            return;

        var startEvt = new ToolCallStartEvent
        {
            ToolName = toolCall.Name,
            CallId = toolCall.CallId,
            Args = toolCall.Arguments ?? new Dictionary<string, object?>(),
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (data.Channel != null)
            await _channelEventBroadcaster.BroadcastChannelToolCallStartAsync(data.Channel.Id, startEvt);
        else if (data.DmChannel != null)
            await _channelEventBroadcaster.BroadcastDmToolCallStartAsync(data.DmChannel.Id, startEvt);
    }

    private async Task BroadcastToolCallCompleted(AgentRunData data, AocFunctionCallContent toolCall, AocFunctionResultContent result)
    {
        if (_channelEventBroadcaster == null || (data.Channel == null && data.DmChannel == null))
            return;

        var toolCallResultObj = result.Result as ToolCallResult;
        var isError = toolCallResultObj?.IsError ?? false;
        var evt = new ToolCallCompletedEvent
        {
            ToolName = toolCall.Name,
            CallId = toolCall.CallId,
            Args = toolCall.Arguments ?? new Dictionary<string, object?>(),
            Result = toolCallResultObj?.Result,
            IsError = isError,
            Timestamp = DateTimeOffset.UtcNow,
        };

        if (data.Channel != null)
            await _channelEventBroadcaster.BroadcastChannelToolCallCompletedAsync(data.Channel.Id, evt);
        else if (data.DmChannel != null)
            await _channelEventBroadcaster.BroadcastDmToolCallCompletedAsync(data.DmChannel.Id, evt);
    }

}

