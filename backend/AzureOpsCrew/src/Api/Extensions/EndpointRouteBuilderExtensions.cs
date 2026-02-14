using AzureOpsCrew.Api.Endpoints.Dtos.AGUI;
using Microsoft.Agents.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

namespace AzureOpsCrew.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapAgUI(this IEndpointRouteBuilder app)
    {
        var chatClient = app.ServiceProvider.GetRequiredService<IChatClient>();

        const string toolHint =
            " When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), " +
            "use them proactively to present information visually instead of plain text. " +
            "For example, show pipeline stages as a visual card, display work items in a list, or present metrics in a dashboard-style card.";

        var manager = chatClient.AsAIAgent(
            name: "Manager",
            instructions: "You are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise." + toolHint);

        var azureDevOps = chatClient.AsAIAgent(
            name: "Azure DevOps",
            instructions: "You are an Azure DevOps expert. You help with pipelines (YAML and classic), CI/CD, Azure Repos, Boards, Artifacts, Test Plans, and release management. You know branching strategies, approvals, variable groups, service connections, and Azure DevOps REST APIs. Give concrete, step-by-step guidance when asked." + toolHint);

        var azureDev = chatClient.AsAIAgent(
            name: "Azure Dev",
            instructions: "You are an Azure development expert. You help with building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, Azure SDKs, identity (Microsoft Entra ID), storage, messaging, and serverless. You focus on code, configuration, and best practices for Azure-native development." + toolHint);

        // TODO: Replace agentName with agentId
        app.MapPost("/agui/{agentName}", async ([FromRoute] string agentName, [FromBody] RunAgentInput? input, HttpContext context, CancellationToken cancellationToken) =>
        {
            if (input is null)
            {
                return Results.BadRequest();
            }

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

            var aiAgent = agentName.ToLower() switch
            {
                "manager" => manager,
                "azure-devops" => azureDevOps,
                "azure-dev" => azureDev,
                _ => null,
            };

            if (aiAgent is null)
            {
                return Results.BadRequest($"Unknown agent name: {agentName}");
            }

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
        });
    }
}
