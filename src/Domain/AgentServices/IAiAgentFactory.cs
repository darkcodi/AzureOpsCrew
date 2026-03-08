using AzureOpsCrew.Domain.Agents;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.AgentServices
{
    public interface IAiAgentFactory
    {
        public AIAgent Create(IChatClient client, AgentRunData data);
        public string PreparePrompt(AgentRunData data);
    }
}
