using AzureOpsCrew.Domain.AgentManagements;
using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentManagements.Microsoft
{
    public class Local1AgentFactory : IAgentProviderFactory
    {
        public Task<string> Create(AgentInfo info, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid().ToString("D"));
    }
}
