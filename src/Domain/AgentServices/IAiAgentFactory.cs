using AzureOpsCrew.Domain.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.AgentServices
{
    //!!! DRAFT !!!
    public interface IAiAgentFactory
    {
        public AIAgent Create(IChatClient chatClient, Agent agent, //+ agent mcp aggregate roots
            IReadOnlyList<AITool> extraTools, AdditionalPropertiesDictionary additionalPropertiesDictionary);
    }
}
