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
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Utils;
using Temporalio.Client;
using Worker.Models;
using Worker.Models.Content;
using Worker.Workflows;

namespace AzureOpsCrew.Api.Endpoints;

public static class ChannelAgUiEndpoints
{
    public static void MapAllAgUi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/agents/{id}/agui", async (
                [FromRoute(Name = "id")] Guid agentId,
                [FromBody] RunAgentInput? input,
                AzureOpsCrewContext dbContext,
                HttpContext context,
                CancellationToken cancellationToken) =>
        {
            if (input is null) return Results.BadRequest();
            Log.Information("Received AG-UI event for agent with id {AgentId} with threadId {ThreadId} and runId {RunId}", agentId, input.ThreadId, input.RunId);
            Log.Information("Input: {Input}", JsonConvert.SerializeObject(input));

            var jsonOptions = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var userInput = input.Messages.AsChatMessages(jsonSerializerOptions).LastOrDefault()?.Text;

            var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

            await AgentCoordinatorWorkflow.EnsureCoordinatorStartedAsync(client, agentId);
            // await CronTriggerWorkflow.EnsureCronScheduleAsync(client, agentId);

            // Create run options with AG-UI context in AdditionalProperties
            // var clientTools = input.Tools?.AsAITools().ToList();
            // var runOptions = new ChatClientAgentRunOptions
            // {
            //     ChatOptions = new ChatOptions
            //     {
            //         Tools = clientTools,
            //         AdditionalProperties = new AdditionalPropertiesDictionary
            //         {
            //             ["ag_ui_state"] = input.State,
            //             ["ag_ui_context"] = input.Context?.Select(c => new KeyValuePair<string, string>(c.Description, c.Value)).ToArray(),
            //             ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
            //             ["ag_ui_thread_id"] = input.ThreadId,
            //             ["ag_ui_run_id"] = input.RunId
            //         }
            //     }
            // };

            var trigger = new TriggerEvent(
                TriggerId: Guid.NewGuid(),
                Source: TriggerSource.Dm,
                CreatedAt: DateTime.UtcNow,
                Text: userInput);

            var handle = client.GetWorkflowHandle<AgentCoordinatorWorkflow>(AgentCoordinatorWorkflow.CoordinatorWorkflowId(agentId));
            await handle.ExecuteUpdateAsync(wf => wf.EnqueueAsync(trigger));

            var runEvents = GetDmEventsAsync(agentId, dbContext, cancellationToken);
            var sseLogger = context.RequestServices.GetRequiredService<ILogger<AGUIServerSentEventsResult>>();
            return new AGUIServerSentEventsResult(runEvents, sseLogger, jsonSerializerOptions);
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
            Log.Information("Received AG-UI event for channel with id {ChannelId} with threadId {ThreadId} and runId {RunId}", channelId, input.ThreadId, input.RunId);
            Log.Information("Input: {Input}", JsonConvert.SerializeObject(input));

            var jsonOptions = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

            var messages = input.Messages.AsChatMessages(jsonSerializerOptions);
            var clientTools = input.Tools?.AsAITools().ToList();

            // 1) Load channel + participants
            var channel = await dbContext.Channels.SingleOrDefaultAsync(c => c.Id == channelId, cancellationToken);
            if (channel is null)
                return Results.BadRequest($"Unknown channel with id: {channelId}");

            var agendIds = channel.AgentIds.Select(Guid.Parse).ToList();
            if(!agendIds.Any())
                return Results.BadRequest($"Channel with id {channelId} has no agents added");

            var agents = await dbContext.Agents
                .Where(a => agendIds.Contains(a.Id))
                .ToListAsync(cancellationToken);

            if (agents.Count != agendIds.Count)
                return Results.BadRequest("Some agents was not found.");

            var providerIds = agents.Select(a => a.ProviderId).Distinct().ToList();
            var providers = await dbContext.Providers
                .Where(p => providerIds.Contains(p.Id))
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

    private static async Task<DateTime> GetLastMessageTimestampAsync(Guid agentId, AzureOpsCrewContext context, CancellationToken ct)
    {
        var lastMessage = await context.LlmChatMessages
            .Where(m => m.AgentId == agentId)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefaultAsync(ct);
        return lastMessage?.CreatedAt.DateTime ?? DateTime.MinValue;
    }

    // Periodically (once in 1sec) pulls new events from DB related to the agent and yields them.
    // ToDo: Replace with more efficient pub/sub mechanism
    private static async IAsyncEnumerable<BaseEvent> GetDmEventsAsync(Guid agentId, AzureOpsCrewContext context, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var newMessages = await context.LlmChatMessages
                .Where(m => m.AgentId == agentId)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync(ct);
            foreach (var newMessage in newMessages)
            {
                var baseEvent = MapToBaseEvent(newMessage);
                if (baseEvent != null)
                {
                    yield return baseEvent;
                }
            }

            await ResultWrapper.Wrap(() => Task.Delay(TimeSpan.FromSeconds(1), ct));
        }
    }

    private static BaseEvent? MapToBaseEvent(LlmChatMessage message)
    {
        var aiContent = JsonConvert.DeserializeObject<AocAiContent>(message.ContentJson);
        switch (aiContent)
        {
            case AocRunStart runStart:
                return new RunStartedEvent { RunId = runStart.RunId, ThreadId = runStart.ThreadId };
            case AocRunFinished runFinished:
                return new RunFinishedEvent { RunId = runFinished.RunId, ThreadId = runFinished.ThreadId, Result = runFinished.Result };
            case AocRunError runError:
                return new RunErrorEvent { Message = runError.Message };
            case AocTextContent textContent:
                return new TextMessageContentEvent { MessageId = message.Id.ToString(), Delta = textContent.Text };
            case AocFunctionCallContent functionCallContent:
                return new ToolCallStartEvent { ToolCallId = functionCallContent.CallId, ToolCallName = functionCallContent.Name };
            case AocFunctionResultContent functionResultContent:
                // ToDo: Maybe return ToolCallEndEvent?
                return new ToolCallResultEvent { ToolCallId = functionResultContent.CallId, Content = functionResultContent.Result?.ToString() ?? "<null>" };
            default:
                return null;
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
