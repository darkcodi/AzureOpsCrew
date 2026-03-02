using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Setup.Seeds
{
    public class Seeder
    {
        private readonly AzureOpsCrewContext _context;
        private readonly SeederOptions _seederOptions;

        public Seeder(AzureOpsCrewContext context, SeederOptions seederOptions)
        {
            _context = context;
            _seederOptions = seederOptions;
        }

        public async Task Seed()
        {
            var providerId = Guid.Parse("5f4e3d10-0123-4000-9abc-def123456789");
            var provider = new Provider(providerId,
                name: "Azure OpenAI", ProviderType.AzureFoundry, apiKey: _seederOptions.AzureFoundrySeed.Key,
                apiEndpoint: _seederOptions.AzureFoundrySeed.ApiEndpoint,
                selectedModels: "[\"gpt-5-2-chat\"]", defaultModel: "gpt-5-2-chat");
            await AddProviderIfNotExists(provider);

            var managerId = Guid.Parse("6a5d8a20-1234-4000-a1b2-c3d4e5f6a7b8");
            var azDevOpsId = Guid.Parse("7b6e9b30-2345-4111-b2c3-d4e5f6a7b8c9");
            var azDevId = Guid.Parse("8c7f0c40-3456-4222-c3d4-e5f6a7b8c9d0");
            var generalChatId = Guid.Parse("a5d8a20a-1234-4000-a1b2-c3d4e5f6a7b9");

            var agents = new[]
            {
                new Agent(managerId,
                    new AgentInfo("Manager",
                        "You are a Manager AI assistant. You help with planning, priorities, resource allocation, team coordination, and delivery. You think in terms of goals, milestones, risks, and stakeholder communication. Keep answers actionable and concise.",
                        "gpt-5-2-chat")
                        {
                            Description = "Helps with planning, priorities, resource allocation, team coordination, and delivery",
                            AvailableTools = Array.Empty<AgentTool>()
                        },
                    provider.Id, "manager", "#43b581"
                ),

                new Agent(
                    azDevOpsId,
                    new AgentInfo(
                        "Azure DevOps",
                        "You are an Azure DevOps expert. You help with pipelines (YAML and classic), CI/CD, Azure Repos, Boards, Artifacts, Test Plans, and release management. You know branching strategies, approvals, variable groups, service connections, and Azure DevOps REST APIs. Give concrete, step-by-step guidance when asked.",
                        "gpt-5-2-chat")
                        {
                            Description = "Expert in Azure DevOps pipelines, CI/CD, repos, boards, artifacts, and release management",
                            AvailableTools = Array.Empty<AgentTool>()
                        },
                    provider.Id, "azure-devops", "#0078d4"),

                new Agent(azDevId,
                    new AgentInfo(
                        "Azure Dev",
                        "You are an Azure development expert. You help with building and deploying apps on Azure: App Service, Functions, Container Apps, AKS, Azure SDKs, identity (Microsoft Entra ID), storage, messaging, and serverless. You focus on code, configuration, and best practices for Azure-native development.",
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

            // Create the General channel with its corresponding chat
            var generalChannel = new Channel(generalChatId, "General")
            {
                Description = "General discussion and collaboration",
                ConversationId = null,
                AgentIds = agents.Select(a => a.Id.ToString()).ToArray(),
                DateCreated = DateTime.UtcNow
            };
            await AddChannelWithChatIfNotExists(generalChannel, agents.Select(a => a.Id).ToArray());

            var defaultUser = new User(
                "AzureOpsCrew@mail.xyz",
                "AZUREOPSCREW@MAIL.XYZ",
                "AQAAAAIAAYagAAAAEHds/S4gmNc0Cf04kSQ5E+g2anSh8VUU/xSrmiNqJiq4APpch0OhtXvIWF9wsTf+Rg==", // Pass1234
                "AzureOpsCrew");
            defaultUser.Id = Guid.Parse("EBB8CF5F-CA75-49C0-BED2-91C2DCCAB415");
            await AddUserIfNotExists(defaultUser);

            // Seed DM channels for all agents
            await SeedDmChannels(defaultUser.Id, new[] { managerId, azDevOpsId, azDevId });

            await _context.SaveChangesAsync();
        }

        private async Task SeedDmChannels(Guid userId, Guid[] agentIds)
        {
            // User-to-Agent DMs for each agent
            var dmIdBase = Guid.Parse("b1c2d3e4-5678-90ab-cdef-123456789012");

            foreach (var (agentId, index) in agentIds.Select((id, i) => (id, i)))
            {
                var dmId = CreateDmId(dmIdBase, index);

                var existingDm = await _context.Dms
                    .AsNoTracking()
                    .AnyAsync(dm => dm.Id == dmId);

                if (!existingDm)
                {
                    var userAgentDm = new DirectMessageChannel
                    {
                        Id = dmId,
                        User1Id = userId,
                        Agent1Id = agentId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Dms.Add(userAgentDm);
                }
            }
        }

        private static Guid CreateDmId(Guid baseId, int index)
        {
            var bytes = baseId.ToByteArray();
            bytes[15] = (byte)index; // Modify last byte to create unique IDs
            return new Guid(bytes);
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

        private async Task AddChannelWithChatIfNotExists(Channel channel, Guid[] participantIds)
        {
            var channelExists = await _context.Set<Channel>()
                .AsNoTracking()
                .AnyAsync(c => c.Id == channel.Id);

            if (!channelExists)
            {
                await _context.Set<Channel>().AddAsync(channel);
            }
        }

        private async Task AddUserIfNotExists(User user)
        {
            var exists = await _context.Set<User>()
                .AsNoTracking()
                .AnyAsync(u => u.NormalizedEmail == user.NormalizedEmail);

            if (!exists)
                _context.Add(user);
        }
    }
}
