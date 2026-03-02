using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Setup.Seeds
{
    public class Seeder
    {
        private readonly AzureOpsCrewContext _context;
        private readonly SeederOptions _seederOptions;
        private readonly IPasswordHasher<User> _passwordHasher;

        public Seeder(AzureOpsCrewContext context, SeederOptions seederOptions, IPasswordHasher<User> passwordHasher)
        {
            _context = context;
            _seederOptions = seederOptions;
            _passwordHasher = passwordHasher;
        }

        public async Task Seed()
        {
            const int clientId = 1;

            // Seed demo user (clientId = 1)
            await SeedDemoUser();

            // Seed OpenAI provider
            var providerId = Guid.Parse("5f4e3d10-0123-4000-9abc-def123456789");
            var openAiApiKey = _seederOptions.OpenAiApiKey ?? "";
            var provider = new Provider(providerId, clientId,
                name: "OpenAI", ProviderType.OpenAI, apiKey: openAiApiKey,
                defaultModel: "gpt-4o-mini",
                selectedModels: "[\"gpt-4o-mini\"]");
            await AddProviderIfNotExists(provider);

            const string model = "gpt-4o-mini";
            var managerId = Guid.Parse("6a5d8a20-1234-4000-a1b2-c3d4e5f6a7b8");
            var azDevOpsId = Guid.Parse("7b6e9b30-2345-4111-b2c3-d4e5f6a7b8c9");
            var azDevId = Guid.Parse("8c7f0c40-3456-4222-c3d4-e5f6a7b8c9d0");
            var opsRoomId = Guid.Parse("a5d8a20a-1234-4000-a1b2-c3d4e5f6a7b9");

            var agents = new[]
            {
                new Agent(managerId, clientId,
                    new AgentInfo("Manager",
                        @"You are the Manager agent in the Azure Ops Crew — a multi-agent team that manages Azure infrastructure.
Your role: coordinate the team, plan tasks, prioritize work, track progress, and communicate with the human operator.

RESPONSIBILITIES:
- Break down the human's request into actionable steps
- Delegate Azure resource tasks to Azure Dev and pipeline/board tasks to Azure DevOps
- Summarize progress and next steps
- Ask the human for approval before destructive operations

TOOLS: When you have visual tools (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information as interactive cards instead of plain text.

Keep answers concise and actionable. Always think step-by-step.",
                        model)
                    {
                        Description = "Coordinates the team, plans tasks, delegates work, and reports progress",
                        AvailableTools = Array.Empty<AgentTool>()
                    },
                    provider.Id, "manager", "#43b581"
                ),

                new Agent(azDevOpsId, clientId,
                    new AgentInfo("Azure DevOps",
                        @"You are the Azure DevOps agent in the Azure Ops Crew — a multi-agent team that manages Azure infrastructure.
Your role: manage Azure DevOps resources including pipelines, repos, boards, work items, and releases.

RESPONSIBILITIES:
- Query and manage CI/CD pipelines (trigger builds, check status, view logs)
- Manage work items, boards, and sprints
- Manage repositories, branches, and pull requests
- Handle service connections and variable groups
- Report pipeline and deployment status

You have access to Azure DevOps MCP tools. Use them to fetch real data.

TOOLS: When you have visual tools (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information as interactive cards.

Be precise, use real data from tools, and keep responses concise.",
                        model)
                    {
                        Description = "Expert in Azure DevOps: pipelines, CI/CD, repos, boards, work items, and releases",
                        AvailableTools = Array.Empty<AgentTool>()
                    },
                    provider.Id, "azure-devops", "#0078d4"),

                new Agent(azDevId, clientId,
                    new AgentInfo("Azure Dev",
                        @"You are the Azure Dev agent in the Azure Ops Crew — a multi-agent team that manages Azure infrastructure.
Your role: manage Azure cloud resources — create, configure, monitor, and troubleshoot.

RESPONSIBILITIES:
- Query and manage Azure resources (VMs, App Services, Container Apps, Functions, Storage, etc.)
- Check resource health, metrics, and configurations
- Deploy and scale resources
- Manage resource groups, networking, and security
- Troubleshoot issues and suggest optimizations

You have access to Azure MCP tools. Use them to fetch real data about Azure resources.

TOOLS: When you have visual tools (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), use them proactively to present information as interactive cards.

Be precise, use real data from tools, and keep responses concise.",
                        model)
                    {
                        Description = "Expert in Azure cloud resources: VMs, App Services, Container Apps, AKS, and infrastructure",
                        AvailableTools = Array.Empty<AgentTool>()
                    },
                    provider.Id, "azure-dev", "#00bcf2"
                )
            };

            foreach (var agent in agents)
                await AddAgentIfNotExists(agent);

            var channel = new Channel(opsRoomId, clientId, "Ops Room")
            {
                Description = "Azure infrastructure operations — ask your crew anything",
                ConversationId = null,
                AgentIds = agents.Select(a => a.Id.ToString()).ToArray(),
                DateCreated = DateTime.UtcNow
            };
            await AddChannelIfNotExists(channel);

            await _context.SaveChangesAsync();
        }

        private async Task SeedDemoUser()
        {
            const string demoEmail = "demo@azureopscrew.dev";
            const string demoNormalizedEmail = "DEMO@AZUREOPSCREW.DEV";

            var exists = await _context.Users
                .AsNoTracking()
                .AnyAsync(u => u.NormalizedEmail == demoNormalizedEmail);

            if (!exists)
            {
                var user = new User(
                    email: demoEmail,
                    normalizedEmail: demoNormalizedEmail,
                    passwordHash: string.Empty,
                    displayName: "Demo User");

                var hash = _passwordHasher.HashPassword(user, "AzureOpsCrew2025!");
                user.UpdatePasswordHash(hash);
                user.MarkLogin();
                _context.Users.Add(user);
            }
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
