using AzureOpsCrew.Api.Endpoints.Dtos.AGUI;
using AzureOpsCrew.Api.Extensions;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using OpenAI;
using Serilog;

namespace AzureOpsCrew.Api.Endpoints;

public static class AgentAgUiEndpoints
{
    public static void MapAgentAgUi(this IEndpointRouteBuilder app)
    {
        const string toolHint =
            " When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), " +
            "use them proactively to present information visually instead of plain text. " +
            "For example, show pipeline stages as a visual card, display work items in a list, or present metrics in a dashboard-style card.";

        app.MapPost("/api/agents/{id}/agui", async ([FromRoute(Name = "id")] Guid agentId, [FromBody] RunAgentInput? input, OpenAIClient openIdClient, AzureOpsCrewContext dbContext, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (input is null)
            {
                return Results.BadRequest();
            }

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

            var agentsCount = dbContext.Set<Domain.Agents.Agent>().Count();
            Log.Information("Looking for agent with id {AgentId} among {AgentsCount} agents in the database", agentId, agentsCount);

            var agentIds = dbContext.Set<Domain.Agents.Agent>().Select(a => a.Id).ToList();
            Log.Information("Agent IDs in database: {AgentIds}", string.Join(", ", agentIds));

            // Find Agent
            var agent = dbContext.Set<Domain.Agents.Agent>().SingleOrDefault(a => a.Id == agentId);
            if (agent is null)
            {
                Log.Warning("Unknown agent with id: {AgentId}", agentId);
                return Results.BadRequest($"Unknown agent with id: {agentId}");
            }

            // Create Ai Agent
            var chatClient = openIdClient.GetChatClient(agent.Info.Model).AsIChatClient();

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
        .WithTags("Agents"); ;
    }
}
