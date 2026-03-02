using System.Runtime.CompilerServices;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.Models;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using AzureOpsCrew.Infrastructure.Ai.Tools;
using AzureOpsCrew.Infrastructure.Db;
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
    private readonly IChannelEventBroadcaster? _channelEventBroadcaster;

    public AgentRunService(IServiceProvider serviceProvider)
    {
        _dbContext = serviceProvider.GetRequiredService<AzureOpsCrewContext>();
        _providerFactory = serviceProvider.GetRequiredService<IProviderFacadeResolver>();
        _toolExecutor = serviceProvider.GetRequiredService<ToolExecutor>();
        // Channel event broadcaster is optional - only available for channels, not DMs
        _channelEventBroadcaster = serviceProvider.GetService<IChannelEventBroadcaster>();
    }

    public async Task Run(Guid agentId, Guid chatId, CancellationToken ct)
    {
        Log.Information("[BACKGROUND] Starting agent run: {AgentId}, chat: {ChatId}", agentId, chatId);

        var data = await LoadAgentRunData(agentId, chatId, ct);
        var agentName = data.Agent.Info.Username;

        // Broadcast agent thinking start for channels
        if (data.Channel != null && _channelEventBroadcaster != null)
        {
            await _channelEventBroadcaster.BroadcastAgentThinkingStartAsync(data.Channel.Id, agentId, agentName);
            await _channelEventBroadcaster.BroadcastTypingIndicatorAsync(data.Channel.Id, agentId, agentName, isTyping: true);
        }

        try
        {
            var iteration = 0;
            const int maxIterations = 50;
            // multiple iterations for one run, stops when outputted a final text content
            while (!ct.IsCancellationRequested && iteration < maxIterations)
            {
                iteration++;
                Log.Debug("[BACKGROUND] Agent {AgentId} iteration {Iteration}", agentId, iteration);

                var prompt = await PreparePrompt(data);

                var newAgentThoughts = new List<AocAgentThought>();
                await foreach (var agentThought in CallLlm(data, prompt, ct))
                {
                    newAgentThoughts.Add(agentThought);
                }

                await SaveRawLlmHttpCall(agentId, chatId, newAgentThoughts, ct);
                ConcatTextContent(newAgentThoughts);
                await SaveAgentThoughts(agentId, chatId, newAgentThoughts, ct);

                var toolCallResults = new List<AocAgentThought>();
                await foreach (var toolCallResult in ExecuteToolCalls(data, newAgentThoughts, ct))
                {
                    var toolResultMessage = AocAgentThought.FromContent(toolCallResult, ChatRole.Tool, data.Agent.Info.Username, DateTime.UtcNow);
                    toolCallResults.Add(toolResultMessage);
                }
                await SaveAgentThoughts(agentId, chatId, toolCallResults, ct);

                if (toolCallResults.Count == 0)
                {
                    // If there are no tool calls, we can assume the agent has finished its run after one iteration.
                    // This is a simplification and can be improved by adding explicit signals in the future.

                    // Send last text content to channel or DM
                    var lastTextContent = newAgentThoughts.Select(t => t.ContentDto.ToAocAiContent())
                        .OfType<AocTextContent>()
                        .LastOrDefault();
                    if (lastTextContent != null)
                    {
                        var message = new Message
                        {
                            Id = Guid.NewGuid(),
                            Text = lastTextContent.Text,
                            PostedAt = DateTime.UtcNow,
                            AgentId = agentId,
                            AuthorName = data.Agent.Info.Username,
                        };

                        if (data.Channel != null)
                        {
                            message.ChannelId = data.Channel.Id;
                        }
                        else
                        {
                            message.DmId = data.Dm!.Id;
                        }
                        _dbContext.Messages.Add(message);
                        await _dbContext.SaveChangesAsync(ct);
                        Log.Debug("[BACKGROUND] Saved message for agent {AgentId} to {ChatType}", agentId, data.Channel != null ? "channel" : "DM");

                        // Broadcast the message for channels
                        if (data.Channel != null && _channelEventBroadcaster != null)
                        {
                            await _channelEventBroadcaster.BroadcastMessageAddedAsync(data.Channel.Id, message);
                        }
                    }

                    break;
                }
            }

            if (iteration >= maxIterations)
            {
                Log.Warning("[BACKGROUND] Agent run hit max iterations: {AgentId}, chat: {ChatId}", agentId, chatId);
            }
        }
        finally
        {
            // Broadcast agent thinking end for channels
            if (data.Channel != null && _channelEventBroadcaster != null)
            {
                await _channelEventBroadcaster.BroadcastAgentThinkingEndAsync(data.Channel.Id, agentId, agentName);
                await _channelEventBroadcaster.BroadcastTypingIndicatorAsync(data.Channel.Id, agentId, agentName, isTyping: false);
            }
        }

        Log.Information("[BACKGROUND] Agent run completed: {AgentId}, chat: {ChatId}", agentId, chatId);
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
        var backendTools = BackEndTools.GetDeclarations();
        var frontEndTools = FrontEndTools.GetDeclarations();
        var tools = backendTools
            .Concat(frontEndTools)
            .ToList();

        Log.Debug("[BACKGROUND] Loaded data for agent {AgentId}: {MessageCount} messages, {ThoughtCount} thoughts, {ToolCount} tools",
            agentId, chatMessages.Count, llmThoughts.Count, tools.Count);

        return new AgentRunData
        {
            Agent = agent,
            Provider = provider,
            Channel = channel,
            Dm = dm,
            ChatMessages = chatMessages,
            LlmThoughts = llmThoughts,
            Tools = tools,
        };
    }

    private async Task<string> PreparePrompt(AgentRunData data)
    {
        const string systemPrompt = "You are one of agents in group chat: agents + human. When you have tools available, use them proactively to present information visually instead of plain text. Do NOT issue several tool calls in a row, and always wait for the result of a tool call before issuing another tool call. If you want to issue multiple tool calls, please issue them one by one and wait for the result of each tool call.";

        var beTools = string.Join("\n\n", data.Tools.Where(x => x.ToolType == ToolType.BackEnd).Select(t => t.FormatToolDeclaration()));
        var feTools = string.Join("\n\n", data.Tools.Where(x => x.ToolType == ToolType.FrontEnd).Select(t => t.FormatToolDeclaration()));

        if (string.IsNullOrEmpty(beTools))
        {
            beTools = "No backend tools available.";
        }
        if (string.IsNullOrEmpty(feTools))
        {
            feTools = "No frontend tools available.";
        }

        var prompt = $"""
System prompt:
{systemPrompt}

Available backend tools:
{beTools}

Available frontend tools:
{feTools}

Your username is:
{data.Agent.Info.Username}

Your description is:
{data.Agent.Info.Description}

User prompt:
{data.Agent.Info.Prompt}
""";

        return prompt;
    }

    private async IAsyncEnumerable<AocAgentThought> CallLlm(AgentRunData data, string prompt, [EnumeratorCancellation] CancellationToken ct)
    {
        Log.Debug("[BACKGROUND] Calling LLM for agent {AgentId} with model {Model}", data.Agent.Id, data.Agent.Info.Model);

        var providerFacade = _providerFactory.GetService(data.Provider.ProviderType);
        var chatClient = providerFacade.CreateChatClient(data.Provider, data.Agent.Info.Model, CancellationToken.None);
        var fClient = new FunctionInvokingChatClient(chatClient);
        var chatOptions = new ChatOptions
        {
            Instructions = prompt,
            Tools = data.Tools.Select(x => (AITool)x.ToAiFunctionDeclaration()).ToArray(),
        };
        var chatMessages = data.ChatMessages.Select(m => m.ToChatMessage()).ToList();
        var thoughts = data.LlmThoughts.Select(x => AocAgentThought.FromDomain(x).ToChatMessage()).ToList();
        var allMessages = chatMessages.Concat(thoughts).OrderBy(x => x.CreatedAt).ToList();

        await foreach (ChatResponseUpdate update in fClient.GetStreamingResponseAsync(allMessages, chatOptions, ct))
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

                    // Broadcast text content for channels
                    if (data.Channel != null && _channelEventBroadcaster != null && parsedContent is AocTextContent textContent)
                    {
                        await _channelEventBroadcaster.BroadcastAgentTextContentAsync(
                            data.Channel.Id,
                            data.Agent.Id,
                            data.Agent.Info.Username,
                            textContent.Text,
                            isDelta: true);
                    }

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

    // If there are multiple text content in a row, we want to concat them into one content
    private static void ConcatTextContent(List<AocAgentThought> messages)
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

    private async IAsyncEnumerable<AocFunctionResultContent> ExecuteToolCalls(AgentRunData data,
        List<AocAgentThought> newAgentThoughts, [EnumeratorCancellation] CancellationToken ct)
    {
        var tools = data.Tools;
        var toolCalls = newAgentThoughts
            .Select(m => m.ContentDto.ToAocAiContent())
            .OfType<AocFunctionCallContent>()
            .ToList();

        if (toolCalls.Count > 0)
        {
            Log.Debug("[BACKGROUND] Executing {ToolCount} tool calls for agent {AgentId}", toolCalls.Count, data.Agent.Id);
        }

        // ToDo: Add support for parallel tool calls if needed. For now we execute them sequentially for simplicity.
        foreach (var toolCall in toolCalls)
        {
            if (ct.IsCancellationRequested)
                yield break;

            var toolName = toolCall.Name;
            var toolDeclaration = tools.FirstOrDefault(t => t.Name == toolName);
            if (toolDeclaration is null)
            {
                // If the tool declaration is not found, we return an error result for this tool call.
                // This can happen if the LLM calls a tool that is not declared in the prompt or if there is a typo in the tool name.
                Log.Warning("[BACKGROUND] Tool {ToolName} not found in declarations", toolName);

                // Broadcast tool call error for channels
                if (data.Channel != null && _channelEventBroadcaster != null)
                {
                    await _channelEventBroadcaster.BroadcastToolCallStartAsync(
                        data.Channel.Id, data.Agent.Id, data.Agent.Info.Username, toolName, toolCall.CallId);
                    await _channelEventBroadcaster.BroadcastToolCallEndAsync(
                        data.Channel.Id, data.Agent.Id, data.Agent.Info.Username, toolName, toolCall.CallId,
                        success: false, errorMessage: "Tool not found in declarations");
                }

                yield return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
                continue;
            }

            if (toolDeclaration.ToolType == ToolType.FrontEnd)
            {
                Log.Debug("[BACKGROUND] Front-end tool {ToolName} called, returning empty result", toolName);

                // Broadcast tool call start/end for front-end tools (channels only)
                if (data.Channel != null && _channelEventBroadcaster != null)
                {
                    await _channelEventBroadcaster.BroadcastToolCallStartAsync(
                        data.Channel.Id, data.Agent.Id, data.Agent.Info.Username, toolName, toolCall.CallId);
                    await _channelEventBroadcaster.BroadcastToolCallEndAsync(
                        data.Channel.Id, data.Agent.Id, data.Agent.Info.Username, toolName, toolCall.CallId,
                        success: true);
                }

                // For front-end tools, we can return an empty result immediately since the front-end will handle the rendering based on the tool declaration.
                yield return AocFunctionResultContent.Empty(toolCall.CallId);
                continue;
            }

            // Broadcast tool call start for backend tools (channels only)
            if (data.Channel != null && _channelEventBroadcaster != null)
            {
                await _channelEventBroadcaster.BroadcastToolCallStartAsync(
                    data.Channel.Id, data.Agent.Id, data.Agent.Info.Username, toolName, toolCall.CallId);
            }

            Log.Debug("[BACKGROUND] Executing tool {ToolName}", toolName);
            AocFunctionResultContent? toolCallResult = null;
            Exception? toolError = null;

            try
            {
                toolCallResult = await _toolExecutor.ExecuteTool(toolDeclaration, toolCall);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[BACKGROUND] Error executing tool {ToolName}", toolName);
                toolError = ex;
            }

            // Broadcast tool call end for channels
            if (data.Channel != null && _channelEventBroadcaster != null)
            {
                await _channelEventBroadcaster.BroadcastToolCallEndAsync(
                    data.Channel.Id, data.Agent.Id, data.Agent.Info.Username, toolName, toolCall.CallId,
                    success: toolError == null, errorMessage: toolError?.Message);
            }

            if (toolError != null)
            {
                // Return an error result similar to ToolDoesNotExist pattern
                yield return new AocFunctionResultContent
                {
                    CallId = toolCall.CallId,
                    Result = new ToolCallResult(SerializedResult: $"{{\"ErrorMessage\":\"{toolError.Message}\"}}", IsError: true),
                };
                continue;
            }

            yield return toolCallResult!;
        }
    }
}

public class AgentRunData
{
    public Agent Agent { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public Channel? Channel { get; set; }
    public DirectMessageChannel? Dm { get; set; }
    public List<Message> ChatMessages { get; set; } = null!;
    public List<AgentThought> LlmThoughts { get; set; } = null!;
    public List<ToolDeclaration> Tools { get; set; } = null!;
}
