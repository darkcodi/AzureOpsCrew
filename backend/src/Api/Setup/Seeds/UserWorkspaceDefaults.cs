using System.Security.Cryptography;
using System.Text;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Setup.Seeds;

public static class UserWorkspaceDefaults
{
    private sealed record DefaultAgentTemplate(string Name, string ProviderAgentId, string Color, string Prompt, string Description);

    private static readonly DefaultAgentTemplate[] DefaultAgents =
    [
        new(
            "Manager",
            "manager",
            "#43b581",
            "You are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.",
            "Helps with planning, priorities, resource allocation, team coordination, and delivery"),
        new(
            "Azure DevOps",
            "azure-devops",
            "#0078d4",
            "You are an Azure DevOps expert. You help with pipelines (YAML and classic), CI/CD, Azure Repos, Boards, Artifacts, Test Plans, and release management. You know branching strategies, approvals, variable groups, service connections, and Azure DevOps REST APIs. Give concrete, step-by-step guidance when asked. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.",
            "Expert in Azure DevOps pipelines, CI/CD, repos, boards, artifacts, and release management"),
        new(
            "Azure Dev",
            "azure-dev",
            "#00bcf2",
            "You are an Azure development expert. You help with building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, Azure SDKs, identity (Microsoft Entra ID), storage, messaging, and serverless. You focus on code, configuration, and best practices for Azure-native development. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.",
            "Expert in building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, and more")
    ];

    public static async Task EnsureAsync(
        AzureOpsCrewContext context,
        SeederOptions? seederOptions,
        int clientId,
        CancellationToken cancellationToken)
    {
        var hasChannel = await context.Set<Channel>()
            .AsNoTracking()
            .AnyAsync(c => c.ClientId == clientId, cancellationToken);

        var hasAgent = await context.Set<Agent>()
            .AsNoTracking()
            .AnyAsync(a => a.ClientId == clientId, cancellationToken);

        var hasProvider = await context.Set<Provider>()
            .AsNoTracking()
            .AnyAsync(p => p.ClientId == clientId, cancellationToken);

        if (hasChannel && hasAgent && hasProvider)
            return;

        var provider = await EnsureDefaultProviderAsync(context, seederOptions, clientId, cancellationToken);
        var defaultAgents = await EnsureDefaultAgentsAsync(context, provider, clientId, cancellationToken);
        await EnsureGeneralChannelAsync(context, clientId, defaultAgents, cancellationToken);

        if (!context.ChangeTracker.HasChanges())
            return;

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Parallel requests can race on first-load bootstrap. IDs are deterministic, so
            // a duplicate insert from another request is safe to ignore and retry via query.
            context.ChangeTracker.Clear();
        }
    }

    private static async Task<Provider?> EnsureDefaultProviderAsync(
        AzureOpsCrewContext context,
        SeederOptions? seederOptions,
        int clientId,
        CancellationToken cancellationToken)
    {
        var deterministicProviderId = DeterministicGuid("provider", clientId, "azure-openai");

        var provider = await context.Set<Provider>()
            .SingleOrDefaultAsync(
                p => p.Id == deterministicProviderId ||
                     (p.ClientId == clientId && p.ProviderType == ProviderType.AzureFoundry && p.Name == "Azure OpenAI"),
                cancellationToken);

        if (provider is not null)
            return provider;

        var apiEndpoint = seederOptions?.AzureFoundrySeed?.ApiEndpoint?.Trim();
        var apiKey = seederOptions?.AzureFoundrySeed?.Key?.Trim();

        if (string.IsNullOrWhiteSpace(apiEndpoint) || string.IsNullOrWhiteSpace(apiKey))
            return null;

        provider = new Provider(
            deterministicProviderId,
            clientId,
            name: "Azure OpenAI",
            ProviderType.AzureFoundry,
            apiKey: apiKey,
            apiEndpoint: apiEndpoint,
            selectedModels: "[\"gpt-5-2-chat\"]",
            defaultModel: "gpt-5-2-chat");

        context.Add(provider);
        return provider;
    }

    private static async Task<IReadOnlyList<Agent>> EnsureDefaultAgentsAsync(
        AzureOpsCrewContext context,
        Provider? provider,
        int clientId,
        CancellationToken cancellationToken)
    {
        if (provider is null)
            return [];

        var existingAgents = await context.Set<Agent>()
            .Where(a => a.ClientId == clientId)
            .ToListAsync(cancellationToken);

        var results = new List<Agent>(DefaultAgents.Length);

        foreach (var template in DefaultAgents)
        {
            var deterministicAgentId = DeterministicGuid("agent", clientId, template.ProviderAgentId);
            var existing = existingAgents.FirstOrDefault(a =>
                a.Id == deterministicAgentId || a.ProviderAgentId == template.ProviderAgentId);

            if (existing is not null)
            {
                results.Add(existing);
                continue;
            }

            var agent = new Agent(
                deterministicAgentId,
                clientId,
                new AgentInfo(template.Name, template.Prompt, "gpt-5-2-chat")
                {
                    Description = template.Description,
                    AvailableTools = Array.Empty<AgentTool>()
                },
                provider.Id,
                template.ProviderAgentId,
                template.Color);

            context.Add(agent);
            results.Add(agent);
        }

        return results;
    }

    private static async Task EnsureGeneralChannelAsync(
        AzureOpsCrewContext context,
        int clientId,
        IReadOnlyList<Agent> defaultAgents,
        CancellationToken cancellationToken)
    {
        var deterministicChannelId = DeterministicGuid("channel", clientId, "general");

        var channel = await context.Set<Channel>()
            .SingleOrDefaultAsync(
                c => c.Id == deterministicChannelId ||
                     (c.ClientId == clientId && c.Name == "General"),
                cancellationToken);

        var defaultAgentIds = defaultAgents.Select(a => a.Id.ToString("D")).ToArray();

        if (channel is null)
        {
            channel = new Channel(deterministicChannelId, clientId, "General")
            {
                Description = "General discussion and collaboration",
                ConversationId = null,
                AgentIds = defaultAgentIds,
                DateCreated = DateTime.UtcNow
            };

            context.Add(channel);
            return;
        }

        if ((channel.AgentIds is null || channel.AgentIds.Length == 0) && defaultAgentIds.Length > 0)
            channel.AgentIds = defaultAgentIds;
    }

    private static Guid DeterministicGuid(string scope, int clientId, string key)
    {
        var input = Encoding.UTF8.GetBytes($"aoc-default:{scope}:{clientId}:{key}");
        var hash = SHA256.HashData(input);
        var bytes = new byte[16];
        Array.Copy(hash, bytes, 16);
        return new Guid(bytes);
    }
}
