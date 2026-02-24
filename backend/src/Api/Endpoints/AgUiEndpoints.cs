using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.AGUI;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Domain.AgentServices;
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
using System.Text.Json;
using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Exceptions;
using Worker.Models;
using Worker.Workflows;

namespace AzureOpsCrew.Api.Endpoints;

public static class ChannelAgUiEndpoints
{
    public static void MapAllAgUi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/agents/{id}/agui", async (
                [FromRoute(Name = "id")] Guid agentId,
                [FromBody] RunAgentInput? input,
                IProviderFacadeResolver providerFactory,
                AzureOpsCrewContext dbContext,
                IAiAgentFactory agentFactory,
                HttpContext context,
                CancellationToken cancellationToken) =>
        {
            if (input is null) return Results.BadRequest();
            var userId = context.User.GetRequiredUserId();
            Log.Information("Received AG-UI event for agent with id {AgentId} with threadId {ThreadId} and runId {RunId}", agentId, input.ThreadId, input.RunId);
            Log.Information("Input: {Input}", JsonConvert.SerializeObject(input));

            var jsonOptions = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions);
            var lastMessage = messages.LastOrDefault();
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

            var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

            await EnsureCoordinatorStartedAsync(client, agentId);
            // await EnsureCronScheduleAsync(client, agentId);

            var trigger = new TriggerEvent(
                TriggerId: $"agui-dm-{Guid.NewGuid()}",
                Source: TriggerSource.UserMessage,
                AgentId: agentId,
                Text: lastMessage?.Text);

            var handle = client.GetWorkflowHandle<AgentCoordinatorWorkflow>(CoordinatorWorkflowId(agentId));

            var outcome = await handle.ExecuteUpdateAsync(wf => wf.AskAsync(trigger));

            // // Find Provider
            // var provider = dbContext.Set<Domain.Providers.Provider>().SingleOrDefault(p => p.Id == agent.ProviderId && p.ClientId == userId);
            // if (provider is null)
            // {
            //     Log.Warning("Unknown provider with id: {ProviderId} for agent {AgentId}", agent.ProviderId, agent.Id);
            //     return Results.BadRequest($"Unknown provider with id: {agent.ProviderId}");
            // }
            // Log.Information("Found provider {ProviderId} for agent {AgentId}", provider.Id, agent.Id);
            //
            // // Create Ai Agent
            // var providerService = providerFactory.GetService(provider.ProviderType);
            // var chatClient = providerService.CreateChatClient(provider, agent.Info.Model, cancellationToken);
            //
            // var aiAgent = ChannelAgUiFactory.CreateChannelAgent(agentFactory, chatClient, agent, clientTools, input);
            //
            // // Run the agent and convert to AG-UI events
            // var events = aiAgent.RunStreamingAsync(
            //     messages,
            //     options: runOptions,
            //     cancellationToken: cancellationToken)
            //     .AsChatResponseUpdatesAsync()
            //     .FilterServerToolsFromMixedToolInvocationsAsync(clientTools, cancellationToken)
            //     .AsAGUIEventStreamAsync(
            //         input.ThreadId,
            //         input.RunId,
            //         jsonSerializerOptions,
            //         cancellationToken);
            //
            var sseLogger = context.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            return new AGUIServerSentEventsResult(AsyncEnumerable.Empty<BaseEvent>(), sseLogger, jsonSerializerOptions);
            // return new AGUIServerSentEventsResult(events, sseLogger, jsonSerializerOptions);
        })
        .WithTags("AG-UI")
        .RequireAuthorization();

        app.MapPost("/api/channels/{id:guid}/agui", async (
            [FromRoute(Name = "id")] Guid channelId,
            [FromBody] RunAgentInput? input,
            IProviderFacadeResolver providerFactory,
            AzureOpsCrewContext dbContext,
            IAiAgentFactory agentFactory,
            HttpContext http,
            CancellationToken cancellationToken) =>
        {
            if (input is null) return Results.BadRequest();
            var userId = http.User.GetRequiredUserId();
            Log.Information("Received AG-UI event for channel with id {ChannelId} with threadId {ThreadId} and runId {RunId}", channelId, input.ThreadId, input.RunId);
            Log.Information("Input: {Input}", JsonConvert.SerializeObject(input));

            var jsonOptions = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions);
            var clientTools = input.Tools?.AsAITools().ToList();

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
            var internalAgents = agents
                .Select(a =>
                {
                    var provider = providers.Single(p => p.Id == a.ProviderId);
                    var providerService = providerFactory.GetService(provider.ProviderType);
                    var chatClient = providerService.CreateChatClient(provider, a.Info.Model, cancellationToken);
                    return ChannelAgUiFactory.CreateChannelAgent(agentFactory, chatClient, a, clientTools, input);
                })
                .ToList();

            // 3) Build workflow -> workflow agent
            var workflow = ChannelAgUiFactory.BuildWorkflow(internalAgents);
            var workflowAgent = ChannelAgUiFactory.BuildWorkflowAgent(workflow, channelId);

            // 4) Restore/create session
            AgentSession session;
            if (false)//(channel.ConversationId is not null) // Use SerializedSession or dedicated aggregate root related to ConversationId JsonElement? or string
            {
                session = await workflowAgent.DeserializeSessionAsync(JsonElement.Parse(channel.ConversationId));
            }
            else
            {
                session = await workflowAgent.CreateSessionAsync();
            }

            // 5) Run streaming
            var updates = workflowAgent
                .RunStreamingAsync(messages, session: session, cancellationToken: cancellationToken)
                .AsChatResponseUpdatesAsync()
                .FilterServerToolsFromMixedToolInvocationsAsync(clientTools, cancellationToken);

            var aguiEvents = updates.AsAGUIEventStreamAsync(
                input.ThreadId,
                input.RunId,
                jsonSerializerOptions,
                cancellationToken);

            // 6) Wrap stream to persist session at the end
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
                aguiEvents,//PersistSessionOnCompletion(aguiEvents, cancellationToken),
                sseLogger,
                jsonSerializerOptions);
        })
        .WithTags("AG-UI")
        .RequireAuthorization();
    }

    static string CoordinatorWorkflowId(Guid agentId) => $"agent:{agentId}";
    static string CronWorkflowId(Guid agentId) => $"agent:{agentId}:cron";

    static async Task EnsureCoordinatorStartedAsync(TemporalClient client, Guid agentId)
    {
        var coordinationWorkflowId = CoordinatorWorkflowId(agentId);

        try
        {
            await client.StartWorkflowAsync(
                (AgentCoordinatorWorkflow wf) => wf.RunAsync(new CoordinatorInit(agentId)),
                new(id: coordinationWorkflowId, taskQueue: "aoc-agent-task-queue"));
        }
        catch (WorkflowAlreadyStartedException)
        {
            // fine (desired) outcome - workflow is already running
        }
    }

    static async Task EnsureCronScheduleAsync(TemporalClient client, Guid agentId)
    {
        var cronWorkflowId = CronWorkflowId(agentId);

        try
        {
            // Fires every 5 minutes (use Cron expressions if you prefer; intervals are simplest)
            await client.CreateScheduleAsync(
                cronWorkflowId,
                new Schedule(
                    Action: ScheduleActionStartWorkflow.Create(
                        (CronTriggerWorkflow wf) => wf.RunAsync(new CronTriggerInput(agentId)),
                        new(id: $"cron-trigger:{agentId}", taskQueue: "aoc-agent-task-queue")),
                    Spec: new()
                    {
                        Intervals = new List<ScheduleIntervalSpec> { new(Every: TimeSpan.FromMinutes(5)) }
                    }));
        }
        catch (ScheduleAlreadyRunningException)
        {
            // schedule already exists
        }
    }
}

public static class ChannelAgUiFactory
{
    public static Workflow BuildWorkflow(IReadOnlyList<AIAgent> agents)
    {
        return AgentWorkflowBuilder
        .CreateGroupChatBuilderWith(chatAgents => new RoundRobinGroupChatManager(agents)
        {
            MaximumIterationCount = 3
        })
        .AddParticipants(agents)
        .Build();
    }

    public static AIAgent BuildWorkflowAgent(Workflow workflow, Guid channelId)
    {
        return workflow.AsAgent(
            id: channelId.ToString(),
            name: $"channel-{channelId}",
            includeExceptionDetails: false
        // , includeWorkflowOutputsInResponse: true  // idk
        );
    }

    public static AIAgent CreateChannelAgent(
        IAiAgentFactory factory,
        IChatClient chatClient,
        Domain.Agents.Agent agentEntity,
        IReadOnlyList<AITool>? clientTools,
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

        return factory.Create(chatClient, agentEntity, clientTools?.ToList() ?? [], additionalPropertiesDictionary);
    }
}
