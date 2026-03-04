using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.AGUI;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Api.Mcp;
using AzureOpsCrew.Api.Orchestration;
using AzureOpsCrew.Api.Orchestration.Engine;
using AzureOpsCrew.Api.Settings;
using RunStatus = AzureOpsCrew.Api.Orchestration.RunStatus;
using AzureOpsCrew.Domain.AgentServices;
using AzureOpsCrew.Domain.Execution;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzureOpsCrew.Api.Endpoints;

public static class ChannelAgUiEndpoints
{
    public static void MapAllAgUi(this IEndpointRouteBuilder app)
    {
        const string toolHint =
            " When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), " +
            "use them proactively to present information visually instead of plain text. " +
            "For example, show pipeline stages as a visual card, display work items in a list, or present metrics in a dashboard-style card.";

        app.MapPost("/api/agents/{id}/agui", async ([FromRoute(Name = "id")] Guid agentId, [FromBody] RunAgentInput? input, IProviderFacadeResolver providerFactory, AzureOpsCrewContext dbContext, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (input is null) return Results.BadRequest();
            var userId = context.User.GetRequiredUserId();
            Log.Information("Received AG-UI event for agent with id {AgentId} with threadId {ThreadId} and runId {RunId}", agentId, input.ThreadId, input.RunId);
            Log.Information("Input: {Input}", JsonConvert.SerializeObject(input));

            var jsonOptions = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions);
            var clientTools = input.Tools?.AsAITools().ToList();

            // Create run options with AG-UI context in AdditionalProperties
            var runOptions = new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions
                {
                    Tools = clientTools,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["ag_ui_state"] = input.State,
                        ["ag_ui_context"] = input.Context?.Select(c => new KeyValuePair<string, string>(c.Description, c.Value)).ToArray(),
                        ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
                        ["ag_ui_thread_id"] = input.ThreadId,
                        ["ag_ui_run_id"] = input.RunId
                    }
                }
            };

            // Find Agent
            var agent = dbContext.Set<Domain.Agents.Agent>().SingleOrDefault(a => a.Id == agentId && a.ClientId == userId);
            if (agent is null)
            {
                Log.Warning("Unknown agent with id: {AgentId}", agentId);
                return Results.BadRequest($"Unknown agent with id: {agentId}");
            }
            Log.Information("Found agent {AgentId}", agent.Id);

            // Find Provider
            var provider = dbContext.Set<Domain.Providers.Provider>().SingleOrDefault(p => p.Id == agent.ProviderId && p.ClientId == userId);
            if (provider is null)
            {
                Log.Warning("Unknown provider with id: {ProviderId} for agent {AgentId}", agent.ProviderId, agent.Id);
                return Results.BadRequest($"Unknown provider with id: {agent.ProviderId}");
            }
            Log.Information("Found provider {ProviderId} for agent {AgentId}", provider.Id, agent.Id);

            // Create Ai Agent
            var providerService = providerFactory.GetService(provider.ProviderType);
            var chatClient = providerService.CreateChatClient(provider, agent.Info.Model, cancellationToken);

            var aiAgent = chatClient.AsAIAgent(
                name: agent.Info.Name,
                instructions: agent.Info.Prompt + toolHint);

            // Run the agent and convert to AG-UI events
            var events = aiAgent.RunStreamingAsync(
                messages,
                options: runOptions,
                cancellationToken: cancellationToken)
                .AsChatResponseUpdatesAsync()
                .FilterServerToolsFromMixedToolInvocationsAsync(clientTools, cancellationToken)
                .AsAGUIEventStreamAsync(
                    input.ThreadId,
                    input.RunId,
                    jsonSerializerOptions,
                    cancellationToken);

            var sseLogger = context.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            return new AGUIServerSentEventsResult(events, sseLogger, jsonSerializerOptions);
        })
        .WithTags("AG-UI")
        .RequireAuthorization();

        app.MapPost("/api/channels/{id:guid}/agui", async (
            [FromRoute(Name = "id")] Guid channelId,
            [FromBody] RunAgentInput? input,
            IProviderFacadeResolver providerFactory,
            AzureOpsCrewContext dbContext,
            IAiAgentFactory agentFactory,
            McpToolProvider mcpToolProvider,
            TaskExecutionEngine executionEngine,
            IOptions<OrchestrationSettings> orchOptions,
            ServiceRegistryProvider serviceRegistry,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (input is null) return Results.BadRequest();
            var userId = http.User.GetRequiredUserId();
            var orchSettings = orchOptions.Value;
            Log.Information("Received AG-UI event for channel with id {ChannelId} with threadId {ThreadId} and runId {RunId}", channelId, input.ThreadId, input.RunId);
            Log.Information("Input: {Input}", JsonConvert.SerializeObject(input));

            // Create execution run for tracking (non-blocking — the existing workflow still drives the chat)
            var userRequest = input.Messages.LastOrDefault()?.Content ?? "(no message)";
            ExecutionRun? executionRun = null;
            try
            {
                executionRun = await executionEngine.CreateRunAsync(
                    channelId, userId, input.ThreadId, userRequest, cancellationToken);
                Log.Information("[Engine] Created execution run {RunId} for channel {ChannelId}",
                    executionRun.Id, channelId);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "[Engine] Failed to create execution run — continuing without tracking");
            }

            var jsonOptions = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions).ToList();
            var clientTools = input.Tools?.AsAITools().ToList();

            // Apply context budget management BEFORE creating agents
            // This prevents context_length_exceeded by compacting message history
            var budgetManager = new ContextBudgetManager();
            var originalMessageCount = messages.Count;
            messages = budgetManager.CompactMessages(messages);
            if (messages.Count < originalMessageCount)
            {
                Log.Information("[ContextBudget] Compacted messages from {Original} to {Compacted} to fit budget",
                    originalMessageCount, messages.Count);
            }

            // 1) Load channel + participants
            var channel = await dbContext.Channels.SingleOrDefaultAsync(c => c.Id == channelId && c.ClientId == userId, cancellationToken);
            if (channel is null)
                return Results.BadRequest($"Unknown channel with id: {channelId}");

            var agendIds = channel.AgentIds.Select(Guid.Parse).ToList();
            if(!agendIds.Any())
                return Results.BadRequest($"Channel with id {channelId} has no agents added");

            var agents = await dbContext.Agents
                .Where(a => agendIds.Contains(a.Id) && a.ClientId == userId)
                .ToListAsync(cancellationToken);

            if (agents.Count != agendIds.Count)
                return Results.BadRequest("Some agents was not found.");

            var providerIds = agents.Select(a => a.ProviderId).Distinct().ToList();
            var providers = await dbContext.Providers
                .Where(p => providerIds.Contains(p.Id) && p.ClientId == userId)
                .ToListAsync(cancellationToken);
            if (providers.Count != providerIds.Count)
                return Results.BadRequest("Some providers was not found.");

            // 2) Create internal agents
            //WARNING: ChatClientAgentRunOptions are ignored from input !!! We should keep the context to ourselves
            AIAgent? managerAIAgent = null;
            var internalAgents = new List<AIAgent>();
            foreach (var a in agents)
            {
                var provider = providers.Single(p => p.Id == a.ProviderId);
                var providerService = providerFactory.GetService(provider.ProviderType);
                var chatClient = providerService.CreateChatClient(provider, a.Info.Model, cancellationToken);

                AIAgent agent;
                if (a.ProviderAgentId == "manager")
                {
                    // Manager is a pure router — no tools (no MCP, no memory).
                    // This prevents it from getting stuck in tool-call loops.
                    // Inject service registry and MCP availability info into Manager prompt.
                    agent = ChannelAgUiFactory.CreateManagerAgent(
                        chatClient, a, input, serviceRegistry, mcpToolProvider, orchSettings);
                    managerAIAgent = agent;
                }
                else
                {
                    // Workers get MCP tools + memory tools for actual task execution
                    IReadOnlyList<AITool> mcpTools;
                    try
                    {
                        mcpTools = await mcpToolProvider.GetToolsForAgentAsync(a.ProviderAgentId, cancellationToken);
                        Log.Information("[ContextBudget] Agent {Agent} has {ToolCount} tools assigned", a.Info.Name, mcpTools.Count);
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Failed to load MCP tools for agent {AgentName}, continuing without them", a.Info.Name);
                        mcpTools = [];
                    }

                    var allTools = new List<AITool>();
                    if (clientTools is not null) allTools.AddRange(clientTools);
                    allTools.AddRange(mcpTools);

                    // Log tool schema budget estimate for this agent
                    var toolSchemaTokens = budgetManager.EstimateToolSchemaTokens(allTools);
                    Log.Information("[ContextBudget] Agent {Agent}: {ToolCount} tools, ~{TokenEstimate} tool schema tokens",
                        a.Info.Name, allTools.Count, toolSchemaTokens);

                    agent = ChannelAgUiFactory.CreateChannelAgent(agentFactory, chatClient, a, allTools, input);
                }

                internalAgents.Add(agent);
            }

            // Fallback: use first agent as manager if no "manager" role found
            managerAIAgent ??= internalAgents[0];

            // Log final context budget estimate
            var messageTokens = budgetManager.EstimateTokenCount(messages);
            Log.Information("[ContextBudget] Final estimate: {MessageCount} messages (~{MessageTokens} tokens), {AgentCount} agents ready",
                messages.Count, messageTokens, internalAgents.Count);

            // 3) Create run context for structured tracing
            var runContextRequest = input.Messages.LastOrDefault()?.Content ?? "(no message)";
            var runContext = new RunContext(input.RunId, input.ThreadId, channelId, runContextRequest);
            runContext.TransitionTo(RunStatus.Triaged, "Channel AG-UI request received");

            // 3.1) Parse direct addressing (@DevOps, @Developer, @Manager)
            if (orchSettings.EnableDirectAddressing)
            {
                var lastUserMessage = input.Messages.LastOrDefault()?.Content;
                if (!string.IsNullOrEmpty(lastUserMessage))
                {
                    var (parsedAddress, cleanedMessage) = DirectAddressingHelper.Parse(lastUserMessage, agents.Select(a => a.Info.Name).ToList());
                    if (parsedAddress.IsDirect)
                    {
                        runContext.SetDirectAddress(parsedAddress);
                        Log.Information("[DirectAddress] User addressed @{Agent}: {Message}", 
                            parsedAddress.AddressedTo, cleanedMessage);
                        
                        // Update the last message to remove the @ prefix for cleaner processing
                        var lastMessage = messages.LastOrDefault();
                        if (lastMessage != null && lastMessage.Role == ChatRole.User)
                        {
                            var idx = messages.IndexOf(lastMessage);
                            messages[idx] = new ChatMessage(ChatRole.User, cleanedMessage)
                            {
                                AuthorName = lastMessage.AuthorName
                            };
                        }
                    }
                }
            }

            // 4) Build workflow -> workflow agent (Multi-round Manager-first orchestration)
            var workflow = ChannelAgUiFactory.BuildWorkflow(internalAgents, managerAIAgent, orchSettings, runContext);
            var workflowAgent = ChannelAgUiFactory.BuildWorkflowAgent(workflow, channelId);

            // 5) Create session
            var session = await workflowAgent.CreateSessionAsync();

            // 6) Run streaming
            var updates = workflowAgent
                .RunStreamingAsync(messages, session: session, cancellationToken: cancellationToken)
                .AsChatResponseUpdatesAsync()
                .WithDebugLogging("POST-WORKFLOW", cancellationToken)
                .FilterServerToolsFromMixedToolInvocationsAsync(clientTools, cancellationToken);

            var aguiEvents = updates.AsAGUIEventStreamAsync(
                input.ThreadId,
                input.RunId,
                jsonSerializerOptions,
                cancellationToken);

            // 7) Wrap with termination guard — idle timeout after agents stop producing events
            var guardedEvents = ChannelAgUiFactory.WithTerminationGuard(
                aguiEvents, input.ThreadId, input.RunId, orchSettings, runContext, cancellationToken);

            // 7) Wrap stream to persist session at the end
            //async IAsyncEnumerable<BaseEvent> PersistSessionOnCompletion(
            //    IAsyncEnumerable<BaseEvent> inner,
            //    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
            //{
            //    try
            //    {
            //        await foreach (var e in inner.WithCancellation(token))
            //            yield return e;
            //    }
            //    finally
            //    {
            //        chat.ConversationId = workflowAgent.SerializeSession(session).ToString();

            //        await dbContext.SaveChangesAsync(token);
            //    }
            //}

            var sseLogger = http.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            return new AGUIServerSentEventsResult(
                guardedEvents,
                sseLogger,
                jsonSerializerOptions);
        })
        .WithTags("AG-UI")
        .RequireAuthorization();
    }
}

public static class ChannelAgUiFactory
{
    public static Workflow BuildWorkflow(
        IReadOnlyList<AIAgent> agents,
        AIAgent managerAgent,
        OrchestrationSettings settings,
        RunContext runContext)
    {
        return AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(chatAgents =>
            new MultiRoundGroupChatManager(agents, managerAgent, settings, runContext))
        .AddParticipants(agents)
        .Build();
    }

    public static AIAgent BuildWorkflowAgent(Workflow workflow, Guid channelId)
    {
        return workflow.AsAgent(
            id: channelId.ToString(),
            name: $"channel-{channelId}",
            includeExceptionDetails: true, // Enable for debugging
            includeWorkflowOutputsInResponse: false // Individual agent messages are already streamed live via AGUI events
        );
    }

    /// <summary>
    /// Wraps the AGUI event stream with a termination guard.
    /// Uses idle-based timeout: after an agent finishes speaking (TextMessageEnd),
    /// if no new events arrive within the configured idle timeout, force-closes the stream.
    /// </summary>
    public static async IAsyncEnumerable<BaseEvent> WithTerminationGuard(
        IAsyncEnumerable<BaseEvent> events,
        string threadId,
        string runId,
        OrchestrationSettings settings,
        RunContext runContext,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromMinutes(settings.TotalTimeoutMinutes));

        int messageEndCount = 0;

        await using var enumerator = events.GetAsyncEnumerator(cts.Token);

        while (true)
        {
            BaseEvent? current;
            try
            {
                if (!await enumerator.MoveNextAsync())
                    break;
                current = enumerator.Current;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                Log.Information("[Run {RunId}] Stream timeout after {Count} agent messages", runContext.RunId, messageEndCount);
                runContext.TransitionTo(RunStatus.Resolved, $"Timeout after {messageEndCount} messages");
                break;
            }

            yield return current;

            if (current is RunFinishedEvent or RunErrorEvent)
            {
                runContext.TransitionTo(
                    current is RunErrorEvent ? RunStatus.Failed : RunStatus.Resolved,
                    current is RunErrorEvent ? "Run error" : "Run finished");
                Log.Information("[Run {RunId}] {Summary}", runContext.RunId, runContext.ToSummary());
                yield break;
            }

            if (current is TextMessageEndEvent)
            {
                messageEndCount++;
                cts.CancelAfter(TimeSpan.FromSeconds(settings.IdleTimeoutSeconds));
            }
        }

        // Force close with RunFinished + trace summary
        Log.Information("[Run {RunId}] {Summary}", runContext.RunId, runContext.ToSummary());
        yield return new RunFinishedEvent
        {
            ThreadId = threadId,
            RunId = runId
        };
    }

    public static AIAgent CreateChannelAgent(
        IAiAgentFactory factory,
        IChatClient chatClient,
        Domain.Agents.Agent agentEntity,
        IReadOnlyList<AITool> tools,
        RunAgentInput input)
    {
        var additionalPropertiesDictionary = new AdditionalPropertiesDictionary
        {
            ["ag_ui_state"] = input.State,
            ["ag_ui_context"] = input.Context?
                        .Select(c => new KeyValuePair<string, string>(c.Description, c.Value))
                        .ToArray(),
            ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
            ["ag_ui_thread_id"] = input.ThreadId,
            ["ag_ui_run_id"] = input.RunId
        };

        return factory.Create(chatClient, agentEntity, tools, additionalPropertiesDictionary);
    }

    /// <summary>
    /// Creates the Manager agent WITHOUT any tools (no MCP, no memory).
    /// The Manager is a pure orchestrator — it reads the user's request and delegates
    /// to workers by mentioning their names. No tool calls needed.
    /// Injects service registry summary and MCP availability diagnostics into the prompt
    /// so the Manager can make informed delegation decisions.
    /// </summary>
    public static AIAgent CreateManagerAgent(
        IChatClient chatClient,
        Domain.Agents.Agent agentEntity,
        RunAgentInput input,
        ServiceRegistryProvider serviceRegistry,
        McpToolProvider mcpToolProvider,
        OrchestrationSettings orchSettings)
    {
        var additionalPropertiesDictionary = new AdditionalPropertiesDictionary
        {
            ["ag_ui_state"] = input.State,
            ["ag_ui_context"] = input.Context?
                        .Select(c => new KeyValuePair<string, string>(c.Description, c.Value))
                        .ToArray(),
            ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
            ["ag_ui_thread_id"] = input.ThreadId,
            ["ag_ui_run_id"] = input.RunId
        };

        // Build context blocks for informed delegation
        var registrySummary = serviceRegistry.GetRegistrySummaryForPrompt();
        var mcpDiagnostics = mcpToolProvider.GetAvailabilityDiagnostics();

        var prompt = $@"
You are the Manager of the Azure Ops Crew — a multi-agent team.

Your name is {agentEntity.Info.Name}.

{agentEntity.Info.Prompt}

=== SERVICE REGISTRY ===
{registrySummary}

=== TOOL AVAILABILITY ===
{mcpDiagnostics}

=== ORCHESTRATION LIMITS ===
- Maximum rounds per run: {orchSettings.MaxRoundsPerRun}
- Idle timeout: {orchSettings.IdleTimeoutSeconds}s
- Total timeout: {orchSettings.TotalTimeoutMinutes}min

CRITICAL REMINDERS:
- Your FIRST response MUST include [TRIAGE] + [PLAN] + explicit delegation to workers by name.
- If the user says something like 'devops, check X' or 'list Azure resources', delegate to DevOps IMMEDIATELY.
- You MUST mention worker names (DevOps, Developer) in your response text for delegation to work.
- NEVER stop at just a [TRIAGE] block. ALWAYS continue to [PLAN] and delegation.
- If unsure which worker to use, default to DevOps for infrastructure/Azure/deployment questions.
Always respond in the same language the human uses.
";

        var options = new ChatClientAgentOptions
        {
            Name = agentEntity.Info.Name,
            ChatOptions = new ChatOptions
            {
                Instructions = prompt,
                Temperature = orchSettings.ModelSettings.TryGetValue("manager", out var ms) ? ms.Temperature : 0.1f,
                Tools = [], // Manager is a router — no tools
                AdditionalProperties = additionalPropertiesDictionary
            }
            // No AIContextProviderFactory — Manager doesn't need memory
        };

        return chatClient.AsAIAgent(options);
    }
}

/// <summary>
/// Helper for parsing direct addressing syntax (@DevOps, @Developer, @Manager) from user messages.
/// </summary>
public static class DirectAddressingHelper
{
    // Match @AgentName at the start of the message or after whitespace
    private static readonly Regex DirectAddressPattern = new(
        @"^@(\w+)\s*,?\s*|(?<=\s)@(\w+)\s*,?\s*",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parse a user message for direct addressing.
    /// Returns the DirectAddressing info and the cleaned message (without the @ prefix).
    /// </summary>
    public static (DirectAddressing Address, string CleanedMessage) Parse(string message, IReadOnlyList<string> agentNames)
    {
        if (string.IsNullOrWhiteSpace(message))
            return (new DirectAddressing(), message);

        var match = DirectAddressPattern.Match(message);
        if (!match.Success)
            return (new DirectAddressing(), message);

        // Get the matched agent name
        var matchedName = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

        // Find matching agent (case-insensitive)
        var targetAgent = agentNames.FirstOrDefault(name =>
            name.Equals(matchedName, StringComparison.OrdinalIgnoreCase));

        if (targetAgent == null)
            return (new DirectAddressing(), message);

        // Clean the message by removing the @ prefix
        var cleanedMessage = DirectAddressPattern.Replace(message, "", 1).Trim();

        return (new DirectAddressing
        {
            AddressedTo = targetAgent,
            CleanedMessage = cleanedMessage
        }, cleanedMessage);
    }
}
