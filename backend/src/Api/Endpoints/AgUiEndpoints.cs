using System.Runtime.CompilerServices;
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
            Log.Information("Input: {Input}", JsonSerializer.Serialize(input));

            var threadId = Guid.TryParse(input.ThreadId, out var parsedThreadId) ? parsedThreadId : Guid.NewGuid();
            var runId = Guid.TryParse(input.RunId, out var parsedRunId) ? parsedRunId : Guid.NewGuid();

            var jsonOptions = context.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
            var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;
            var maxDate = await GetLastMessageTimestampAsync(agentId, dbContext, cancellationToken);

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
                ThreadId: threadId,
                RunId: runId,
                Text: userInput);

            var handle = client.GetWorkflowHandle<AgentCoordinatorWorkflow>(AgentCoordinatorWorkflow.CoordinatorWorkflowId(agentId));
            await handle.SignalAsync(wf => wf.EnqueueAsync(trigger));

            // Poll the database for real events
            var runEvents = GetDmEventsAsync(agentId, dbContext, maxDate, cancellationToken);
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
            Log.Information("Input: {Input}", JsonSerializer.Serialize(input));

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
        return lastMessage?.CreatedAt ?? DateTime.MinValue;
    }

    // Periodically (once in 1sec) pulls new events from DB related to the agent and yields them.
    // ToDo: Replace with more efficient pub/sub mechanism
    private static async IAsyncEnumerable<BaseEvent> GetDmEventsAsync(
        Guid agentId,
        AzureOpsCrewContext context,
        DateTime maxDate,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var maxDateLocal = maxDate;
        while (!ct.IsCancellationRequested)
        {
            var newMessages = await context.LlmChatMessages
                .Where(m => m.AgentId == agentId && m.CreatedAt > maxDateLocal)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(ct);
            foreach (var newMessage in newMessages)
            {
                if (newMessage.CreatedAt <= maxDateLocal)
                    continue;

                if (newMessage.CreatedAt > maxDateLocal)
                    maxDateLocal = newMessage.CreatedAt;

                var baseEvents = MapToBaseEvents(newMessage);
                foreach (var baseEvent in baseEvents)
                {
                    yield return baseEvent;

                    // End the stream on RUN_FINISHED or RUN_ERROR
                    // RUN_ERROR is now terminal since we don't send RUN_FINISHED after errors
                    if (baseEvent is RunFinishedEvent or RunErrorEvent)
                    {
                        yield break;
                    }
                }
            }

            await ResultWrapper.Wrap(() => Task.Delay(TimeSpan.FromSeconds(1), ct));
        }
    }

    private static List<BaseEvent> MapToBaseEvents(LlmChatMessage message)
    {
        if (message.Role == ChatRole.User)
        {
            // we don't need to send user messages to AG-UI, because they are already in the input
            return new List<BaseEvent>();
        }
        var events = new List<BaseEvent>();
        var aiContentDto = new AocAiContentDto
        {
            Content = message.ContentJson,
            ContentType = Enum.Parse<LlmMessageContentType>(message.ContentType.ToString(), ignoreCase: true),
        };
        var aiContent = aiContentDto?.ToAocAiContent();
        switch (aiContent)
        {
            case AocRunStart runStart:
            {
                events.Add(new RunStartedEvent
                {
                    RunId = runStart.RunId.ToString().ToLowerInvariant(),
                    ThreadId = runStart.ThreadId.ToString().ToLowerInvariant()
                });
                break;
            }
            case AocRunFinished runFinished:
            {
                events.Add(new RunFinishedEvent
                {
                    RunId = runFinished.RunId.ToString().ToLowerInvariant(),
                    ThreadId = runFinished.ThreadId.ToString().ToLowerInvariant(), Result = runFinished.Result
                });
                break;
            }
            case AocRunError runError:
            {
                events.Add(new RunErrorEvent { Message = runError.Message });
                break;
            }
            case AocTextContent textContent:
            {
                var messageId = $"{message.AuthorName ?? "assistant"}|chatcmpl-{message.Id.ToString().ToLowerInvariant().Replace("-", "")}";
                events.Add(new TextMessageStartEvent { MessageId = messageId, Role = "assistant" });
                events.Add(new TextMessageContentEvent { MessageId = messageId, Delta = textContent.Text });
                events.Add(new TextMessageEndEvent { MessageId = messageId });
                break;
            }
            case AocFunctionCallContent functionCallContent:
            {
                events.Add(new ToolCallStartEvent
                {
                    ToolCallId = functionCallContent.CallId,
                    ToolCallName = functionCallContent.Name
                });

                if (functionCallContent.Arguments != null)
                {
                    events.Add(new ToolCallArgsEvent
                    {
                        ToolCallId = functionCallContent.CallId,
                        Delta = JsonSerializer.Serialize(
                            functionCallContent.Arguments,
                            new JsonSerializerOptions { WriteIndented = false })
                    });
                }

                events.Add(new ToolCallEndEvent
                {
                    ToolCallId = functionCallContent.CallId
                });
                break;
            }
            case AocFunctionResultContent functionResultContent:
            {
                // ToDo: Maybe return ToolCallEndEvent?
                events.Add(new ToolCallResultEvent
                {
                    ToolCallId = functionResultContent.CallId,
                    Content = functionResultContent.Result?.ToString() ?? "<null>"
                });
                break;
            }
        }

        return events;
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
