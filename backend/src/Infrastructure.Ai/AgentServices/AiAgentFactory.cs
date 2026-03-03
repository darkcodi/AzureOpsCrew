using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.AgentServices
{
    public class AiAgentFactory : IAiAgentFactory
    {
        private const string SystemPrompt =
            "You are one of the agents in the Azure Ops Crew — a disciplined 3-agent engineering team in a group chat with a human operator. " +
            "The Manager orchestrates the team (read-only oversight), DevOps handles infrastructure (Azure/Platform), and Developer handles code (ADO/GitOps). " +
            "Each agent has strict MCP access boundaries. Never attempt to use tools outside your assigned role.";

        private const string ToolHint =
            "When you have visual tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), " +
            "use them proactively to present information as interactive cards instead of plain text. " +
            "When you have MCP tools available, ALWAYS use them to fetch real data from Azure and Azure DevOps. " +
            "NEVER make up or fabricate data — use tools or say you don't have access. " +
            "If you attempt to call a tool and get ACCESS DENIED, stop and report to the Manager — do NOT retry.";

        private readonly AgentAIContextProviderFactory _agentAIContextProviderFactory;

        public AiAgentFactory(AgentAIContextProviderFactory agentAIContextProviderFactory)
        {
            _agentAIContextProviderFactory = agentAIContextProviderFactory;
        }

        public AIAgent Create(IChatClient chatClient, 
            Agent agent,
            IReadOnlyList<AITool> extraTools, 
            AdditionalPropertiesDictionary additionalPropertiesDictionary)
        {
            // Combine client tools (from AG-UI) with any injected MCP tools
            var allTools = extraTools?.ToList() ?? [];

            var prompt = @$"
{SystemPrompt}

Your name is {agent.Info.Name}.

{agent.Info.Prompt}

{ToolHint}

IMPORTANT RULES:
- Be concise. Keep responses under 200 words unless showing tool results.
- When you have MCP tools, use them to get REAL data. Never fabricate data.
- If a task is not in your area of expertise, say so and suggest which agent should handle it.
- Always respond in the same language the human uses.
";

            var options = new ChatClientAgentOptions
            {
                Name = agent.Info.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = prompt,
                    Tools = allTools,
                    AdditionalProperties = additionalPropertiesDictionary
                },
                AIContextProviderFactory = (c, t) => _agentAIContextProviderFactory.Create(c, agent.Id, t)
            };

            return chatClient.AsAIAgent(options);
        }
    }
}
