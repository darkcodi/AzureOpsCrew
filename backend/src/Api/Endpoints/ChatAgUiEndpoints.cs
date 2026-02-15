using AzureOpsCrew.Api.Endpoints.Dtos.AGUI;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using System.Text.Json;

namespace AzureOpsCrew.Api.Endpoints
{
    public static class ChatAgUiEndpoints
    {
        public static void MapChatAgUi(this IEndpointRouteBuilder app)
        {
            //app.MapPost("/api/agents/{chatId}/agui", async (
            app.MapPost("chat/{chatId:guid}/agui/", async (
                [FromRoute] Guid chatId,
                [FromBody] RunAgentInput? input,
                OpenAIClient openAiClient,
                AzureOpsCrewContext dbContext,
                HttpContext http,
                CancellationToken cancellationToken) =>
            {
                if (input is null) return Results.BadRequest();

                var jsonOptions = http.RequestServices.GetRequiredService<IOptions<Microsoft.AspNetCore.Http.Json.JsonOptions>>();
                var jsonSerializerOptions = jsonOptions.Value.SerializerOptions;

                var messages = input.Messages.AsChatMessages(jsonSerializerOptions);
                var clientTools = input.Tools?.AsAITools().ToList();

                // 1) Load chat + participants
                //var chat = await dbContext.Chats.SingleOrDefaultAsync(c => c.Id == chatId, cancellationToken);
                var chat = await dbContext.Chats.FirstOrDefaultAsync(cancellationToken);
                if (chat is null)
                    return Results.BadRequest($"Unknown chat with id: {chatId}");

                var agendIds = chat.AgentIds.Select(Guid.Parse).ToList();
                if(!agendIds.Any())
                    return Results.BadRequest($"Chat with id {chatId} has no agents added");

                var agents = await dbContext.Agents
                    .Where(a => agendIds.Contains(a.Id))
                    .ToListAsync(cancellationToken);

                if (agents.Count != agendIds.Count)
                    return Results.BadRequest($"Some agents was not found.");

                // 2) Create internal agents
                //WARNING: ChatClientAgentRunOptions are ignored from input !!! We should keep the context to ourselves
                var internalAgents = agents
                    .Select(a => ChatAgUiFactory.CreateChatAgent(openAiClient, a, clientTools, input))
                    .ToList();

                // 3) Build workflow -> workflow agent
                var workflow = ChatAgUiFactory.BuildWorkflow(internalAgents);
                var workflowAgent = ChatAgUiFactory.BuildWorkflowAgent(workflow, chatId);

                // 4) Restore/create session
                AgentSession session;
                if (false)//(chat.ConversationId is not null) // Use SerializedSession or dedicated aggregate root related to ConversationId JsonElement? or string
                {
                    session = await workflowAgent.DeserializeSessionAsync(JsonElement.Parse(chat.ConversationId));
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
            .WithTags("ChatAgUi");
        }
    }

    public static class ChatAgUiFactory
    {
        public static Workflow BuildWorkflow(IReadOnlyList<AIAgent> agents)
        {
            // MVP: sequential pipeline.
            // Якщо тобі треба supervisor/handoff — заміниш на HandoffBuilder/GroupChatBuilder.
            return AgentWorkflowBuilder
                .BuildSequential(agents.ToArray());
        }

        public static AIAgent BuildWorkflowAgent(Workflow workflow, Guid chatId)
        {
            // includeWorkflowOutputsInResponse: якщо хочеш, щоб yield_output / outputs теж “вилазили” в response
            // (параметр є в коді WorkflowHostingExtensions) :contentReference[oaicite:10]{index=10}
            return workflow.AsAgent(
                id: chatId.ToString(),
                name: $"chat-{chatId}",
                includeExceptionDetails: false
            // , includeWorkflowOutputsInResponse: true  // якщо доступний у твоїй версії пакета
            );
        }

        public static AIAgent CreateChatAgent(
            OpenAIClient openAiClient,
            Domain.Agents.Agent agentEntity,
            IReadOnlyList<AITool>? clientTools,
            RunAgentInput input)
        {
            const string toolHint =
                " When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), " +
                "use them proactively to present information visually instead of plain text. " +
                "For example, show pipeline stages as a visual card, display work items in a list, or present metrics in a dashboard-style card.";

            var chatClient = openAiClient.GetChatClient(agentEntity.Info.Model).AsIChatClient();

            // Ключ: tools/props задаємо тут, а не через RunOptions (бо workflow.AsAgent може їх не прокинути)
            var options = new ChatClientAgentOptions
            {
                Name = agentEntity.Info.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = agentEntity.Info.Prompt + toolHint,
                    Tools = clientTools?.ToList(),
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["ag_ui_state"] = input.State,
                        ["ag_ui_context"] = input.Context?
                            .Select(c => new KeyValuePair<string, string>(c.Description, c.Value))
                            .ToArray(),
                        ["ag_ui_forwarded_properties"] = input.ForwardedProperties,
                        ["ag_ui_thread_id"] = input.ThreadId,
                        ["ag_ui_run_id"] = input.RunId
                    }
                }
            };

            return chatClient.AsAIAgent(options);
        }

    }
}
