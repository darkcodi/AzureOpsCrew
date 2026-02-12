using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Api.Extensions;

public static class EndpointRouteBuilderExtensions
{
    public static void MapAgUI(this IEndpointRouteBuilder app)
    {
        var chatClient = app.ServiceProvider.GetRequiredService<IChatClient>();

        var manager = chatClient.AsAIAgent(
            name: "Manager",
            instructions: "You are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise.");

        var azureDevOps = chatClient.AsAIAgent(
            name: "Azure DevOps",
            instructions: "You are an Azure DevOps expert. You help with pipelines (YAML and classic), CI/CD, Azure Repos, Boards, Artifacts, Test Plans, and release management. You know branching strategies, approvals, variable groups, service connections, and Azure DevOps REST APIs. Give concrete, step-by-step guidance when asked.");

        var azureDev = chatClient.AsAIAgent(
            name: "Azure Dev",
            instructions: "You are an Azure development expert. You help with building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, Azure SDKs, identity (Microsoft Entra ID), storage, messaging, and serverless. You focus on code, configuration, and best practices for Azure-native development.");

        app.MapAGUI("/agui/manager", manager);
        app.MapAGUI("/agui/azure-devops", azureDevOps);
        app.MapAGUI("/agui/azure-dev", azureDev);
    }
}
