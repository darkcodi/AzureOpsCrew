using AzureOpsCrew.Domain.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.AgentServices
{
    //!!! DRAFT !!!
    public class AiAgentFactory
    {
        private const string ToolHint =
            "When you have tools available (showPipelineStatus, showWorkItems, showResourceInfo, showDeployment, showMetrics), " +
            "use them proactively to present information visually instead of plain text. " +
            "For example, show pipeline stages as a visual card, display work items in a list, or present metrics in a dashboard-style card.";

        public AIAgent Create(IChatClient chatClient, Agent agent, //+ agent mcp aggregate roots
            IReadOnlyList<AITool> extraTools, AdditionalPropertiesDictionary additionalPropertiesDictionary)
        {
            var aITools =
                extraTools
                //Concat(CreateAiTools(agentMcps))
                ;

            var options = new ChatClientAgentOptions
            {
                Name = agent.Info.Name,
                ChatOptions = new ChatOptions
                {
                    Instructions = agent.Info.Prompt + ToolHint,
                    Tools = extraTools?.ToList(),
                    AdditionalProperties = additionalPropertiesDictionary
                }
            };

            return chatClient.AsAIAgent(options);
        }
    }
}
