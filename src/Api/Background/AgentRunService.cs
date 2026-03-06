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

namespace AzureOpsCrew.Api.Background;

public class AgentRunService
{
    private readonly Guid _runId = Guid.NewGuid();
    private readonly AzureOpsCrewContext _dbContext;
    private readonly IProviderFacadeResolver _providerFactory;
    private readonly ToolExecutor _toolExecutor;
    private readonly IAiAgentFactory _aiAgentFactory;
    private readonly IChannelEventBroadcaster? _channelEventBroadcaster;

    public AgentRunService(IServiceProvider serviceProvider)
    {
        _dbContext = serviceProvider.GetRequiredService<AzureOpsCrewContext>();
        _providerFactory = serviceProvider.GetRequiredService<IProviderFacadeResolver>();
        _toolExecutor = serviceProvider.GetRequiredService<ToolExecutor>();
        _aiAgentFactory = serviceProvider.GetRequiredService<IAiAgentFactory>();
        // Event broadcaster is optional - used for both channels and DMs
        _channelEventBroadcaster = serviceProvider.GetService<IChannelEventBroadcaster>();
    }

    public async Task Run(Guid agentId, Guid chatId, CancellationToken ct)
    {
        Log.Information("[BACKGROUND] Starting agent run: {AgentId}, chat: {ChatId}", agentId, chatId);

        try
        {
            var iteration = 0;
            const int maxIterations = 50;
            // multiple iterations for one run, stops when outputted a final text content
            while (!ct.IsCancellationRequested && iteration < maxIterations)
            {
                iteration++;
                Log.Debug("[BACKGROUND] Agent {AgentId} iteration {Iteration}", agentId, iteration);

                // load new DB state for each iteration to get the latest messages and thoughts
                var data = await LoadAgentRunData(agentId, chatId, ct);

                // Make one LLM call
                var newAgentThoughts = new List<AocAgentThought>();
                await foreach (var agentThought in CallLlm(data, ct))
                {
                    newAgentThoughts.Add(agentThought);
                }

                // Compact text content and save to DB
                await SaveRawLlmHttpCall(agentId, chatId, newAgentThoughts, ct);
                SquashTextContent(newAgentThoughts);
                await SaveAgentThoughts(agentId, chatId, newAgentThoughts, ct);

                // Execute tools and save results to DB
                var newToolCallResults = new List<AocAgentThought>();
                var newToolCalls = newAgentThoughts
                    .Select(m => m.ContentDto.ToAocAiContent())
                    .OfType<AocFunctionCallContent>()
                    .ToList();

                // ToDo: Add support for parallel tool calls if needed. For now we execute them sequentially for simplicity.
                foreach (var toolCall in newToolCalls)
                {
                    var toolCallResult = await ExecuteToolCall(toolCall, data);
                    var toolResultMessage = AocAgentThought.FromContent(toolCallResult, ChatRole.Tool, data.Agent.Info.Username, DateTime.UtcNow);
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
            throw;
        }

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
        var backendTools = BackEndTools.All.Select(t => t.GetDeclaration()).ToList();
        var frontEndTools = FrontEndTools.GetDeclarations();
        var tools = backendTools
            .Concat(frontEndTools)
            .ToList();

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
            ParticipantAgents = participantAgents,
        };
    }

    private async IAsyncEnumerable<AocAgentThought> CallLlm(AgentRunData data, [EnumeratorCancellation] CancellationToken ct)
    {
        Log.Debug("[BACKGROUND] Calling LLM for agent {AgentId} with model {Model}", data.Agent.Id, data.Agent.Info.Model);

        var providerFacade = _providerFactory.GetService(data.Provider.ProviderType);
        var chatClient = providerFacade.CreateChatClient(data.Provider, data.Agent.Info.Model, CancellationToken.None);
        var fClient = new FunctionInvokingChatClient(chatClient);

        var aiAgent = _aiAgentFactory.Create(fClient, data);
        var agentSession = await aiAgent.CreateSessionAsync(ct);
        var runOptions = new AgentRunOptions { AllowBackgroundResponses = false };

        var llmThoughtIds = data.LlmThoughts.Select(t => t.Id).ToHashSet();
        var chatMessages = data.ChatMessages.Where(x => !llmThoughtIds.Contains(x.AgentThoughtId ?? Guid.Empty)).Select(m => m.ToChatMessage()).ToList();
        var thoughts = data.LlmThoughts.Select(x => AocAgentThought.FromDomain(x).ToChatMessage()).ToList();
        var allMessages = chatMessages.Concat(thoughts).OrderBy(x => x.CreatedAt).ToList();

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

                    var newMessage = AocAgentThought.FromContent(parsedContent, update.Role ?? ChatRole.Assistant, data.Agent.Info.Username, now);
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

    // If there are multiple text content in a row, we want to squash them into one content
    private static void SquashTextContent(List<AocAgentThought> messages)
    {
        for (int i = messages.Count - 1; i > 0; i--)
        {
            var currentMessage = messages[i];
            var previousMessage = messages[i - 1];

            var currentContent = currentMessage.ContentDto.ToAocAiContent();
            var previousContent = previousMessage.ContentDto.ToAocAiContent();

            if (currentContent is AocTextContent currentTextContent && previousContent is AocTextContent previousTextContent &&
                currentMessage.Role == previousMessage.Role)
            {
                previousTextContent.Text += currentTextContent.Text;
                previousMessage.ContentDto = AocAiContentDto.FromAocAiContent(previousTextContent);
                messages.RemoveAt(i);
            }
            if (currentContent is AocTextReasoningContent currentReasoningContent && previousContent is AocTextReasoningContent previousReasoningContent &&
                currentMessage.Role == previousMessage.Role)
            {
                previousReasoningContent.Text += currentReasoningContent.Text;
                previousMessage.ContentDto = AocAiContentDto.FromAocAiContent(previousReasoningContent);
                messages.RemoveAt(i);
            }
        }
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

    private async Task<AocFunctionResultContent> ExecuteToolCall(AocFunctionCallContent toolCall, AgentRunData data)
    {
        var toolName = toolCall.Name;
        var toolDeclaration = data.Tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
        if (toolDeclaration is null)
        {
            // If the tool declaration is not found, we return an error result for this tool call.
            // This can happen if the LLM calls a tool that is not declared in the prompt or if there is a typo in the tool name.
            Log.Warning("[BACKGROUND] Tool {ToolName} not found in declarations", toolName);

            return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
        }

        if (toolDeclaration.ToolType == ToolType.FrontEnd)
        {
            Log.Debug("[BACKGROUND] Front-end tool {ToolName} called, returning empty result", toolName);

            // For front-end tools, we can return an empty result immediately since the front-end will handle the rendering based on the tool declaration.
            return AocFunctionResultContent.Empty(toolCall.CallId);
        }

        Log.Debug("[BACKGROUND] Executing tool {ToolName}", toolName);
        AocFunctionResultContent? toolCallResult = null;
        Exception? toolError = null;

        try
        {
            toolCallResult = await _toolExecutor.ExecuteTool(data.Agent, toolDeclaration, toolCall);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[BACKGROUND] Error executing tool {ToolName}", toolName);
            toolError = ex;
        }

        if (toolError != null)
        {
            // Return an error result similar to ToolDoesNotExist pattern
            return new AocFunctionResultContent
            {
                CallId = toolCall.CallId,
                Result = new ToolCallResult(CallId: toolCall.CallId, Result: new { ErrorMessage = toolError.Message }, IsError: true),
            };
        }

        return toolCallResult!;
    }
}

