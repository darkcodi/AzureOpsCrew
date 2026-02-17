using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Setup.Seeds
{
    public class Seeder
    {
        private readonly AzureOpsCrewContext _context;

        public Seeder(AzureOpsCrewContext context)
        {
            _context = context;
        }

        public async Task Seed()
        {
            const int clientId = 1;

            var providerId = Guid.Parse("5f4e3d10-0123-4000-9abc-def123456789");
            var provider = new Provider(providerId, clientId,
                name: "Azure OpenAI", ProviderType.AzureFoundry, apiKey: "YOUR_API_KEY",
                apiEndpoint: "https://azureopscrewglobfoundry.openai.azure.com/",
                selectedModels: "[\"gpt-5-2-chat\"]", defaultModel: "gpt-5-2-chat");
            await AddProviderIfNotExists(provider);

            var managerId = Guid.Parse("6a5d8a20-1234-4000-a1b2-c3d4e5f6a7b8");
            var azDevOpsId = Guid.Parse("7b6e9b30-2345-4111-b2c3-d4e5f6a7b8c9");
            var azDevId = Guid.Parse("8c7f0c40-3456-4222-c3d4-e5f6a7b8c9d0");
            var generalChatId = Guid.Parse("a5d8a20a-1234-4000-a1b2-c3d4e5f6a7b9");

            var agents = new[]
            {
                new Agent(managerId, clientId,
                    new AgentInfo("Manager",
                        "You are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.",
                        "gpt-5-2-chat")
                        {
                            Description = "Helps with planning, priorities, resource allocation, team coordination, and delivery",
                            AvailableTools = Array.Empty<AgentTool>()
                        },
                    provider.Id, "manager", "#43b581"
                ),

                new Agent(
                    azDevOpsId, clientId,
                    new AgentInfo(
                        "Azure DevOps",
                        "You are an Azure DevOps expert. You help with pipelines (YAML and classic), CI/CD, Azure Repos, Boards, Artifacts, Test Plans, and release management. You know branching strategies, approvals, variable groups, service connections, and Azure DevOps REST APIs. Give concrete, step-by-step guidance when asked. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.",
                        "gpt-5-2-chat")
                        {
                            Description = "Expert in Azure DevOps pipelines, CI/CD, repos, boards, artifacts, and release management",
                            AvailableTools = Array.Empty<AgentTool>()
                        },
                    provider.Id, "azure-devops", "#0078d4"),

                new Agent(azDevId, clientId,
                    new AgentInfo(
                        "Azure Dev",
                        "You are an Azure development expert. You help with building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, Azure SDKs, identity (Microsoft Entra ID), storage, messaging, and serverless. You focus on code, configuration, and best practices for Azure-native development. When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information visually instead of plain text.",
                        "gpt-5-2-chat")
                        {
                            Description = "Expert in building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, and more",
                            AvailableTools = Array.Empty<AgentTool>()
                        },
                    provider.Id, "azure-dev", "#00bcf2"
                )
            };

            foreach (var agent in agents)
                await AddAgentIfNotExists(agent);

            var channel = new Channel(generalChatId, clientId, "General")
            {
                Description = "General discussion and collaboration",
                ConversationId = null,
                AgentIds = agents.Select(a => a.Id.ToString()).ToArray(),
                DateCreated = DateTime.UtcNow
            };
            await AddChannelIfNotExists(channel);

            await _context.SaveChangesAsync();
        }

        private async Task AddProviderIfNotExists(Provider provider)
        {
            var exists = await _context.Set<Provider>()
                .AsNoTracking()
                .AnyAsync(p => p.Id == provider.Id);

            if (!exists)
                _context.Add(provider);
        }

        private async Task AddAgentIfNotExists(Agent agent)
        {
            var exists = await _context.Set<Agent>()
                .AsNoTracking()
                .AnyAsync(a => a.Id == agent.Id);

            if (!exists)
                _context.Add(agent);
        }

        private async Task AddChannelIfNotExists(Channel channel)
        {
            var exists = await _context.Set<Channel>()
                .AsNoTracking()
                .AnyAsync(c => c.Id == channel.Id);

            if (!exists)
                _context.Add(channel);
        }
    }
}
