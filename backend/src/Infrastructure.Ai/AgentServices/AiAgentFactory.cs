using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.AgentServices
{
    //!!! DRAFT !!!
    public class AiAgentFactory : IAiAgentFactory
    {
        private const string SystemPrompt =
            "You are one of agents in group chat: agents + human.";

        private const string ToolHint =
            "When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), " +
            "use them proactively to present information visually instead of plain text. " +
            "For example, show pipeline stages as a visual card, display work items in a list, or present metrics in a dashboard-style card.";

        private readonly AgentAIContextProviderFactory _agentAIContextProviderFactory;

        public AiAgentFactory(AgentAIContextProviderFactory agentAIContextProviderFactory)
        {
            _agentAIContextProviderFactory = agentAIContextProviderFactory;
        }

        public AIAgent Create(IChatClient chatClient, 
            Agent agent, //+ agent mcp aggregate roots
            IReadOnlyList<AITool> extraTools, 
            AdditionalPropertiesDictionary additionalPropertiesDictionary)//change to dictionary, concat and create AdditionalPropertiesDictionary here
        {
            var aITools =
                extraTools
                //Concat(CreateAiTools(agentMcps))
                ;

            var prompt = @$"
                    {SystemPrompt}

                    Your name is {agent.Info.Name}

                    {ToolHint}
                ";

            var options = new ChatClientAgentOptions
            {
                Name = agent.Info.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = prompt,
                    Tools = extraTools?.ToList(),
                    AdditionalProperties = additionalPropertiesDictionary
                },
                AIContextProviderFactory = (c, t) => _agentAIContextProviderFactory.Create(c, agent.Id, t)
            };

            return chatClient.AsAIAgent(options);
        }
    }
}
