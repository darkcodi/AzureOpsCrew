using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Domain.AgentManagements
{
    public interface IAgentFactory
    {
        public Task<string> Create(Provider provider, AgentInfo info, CancellationToken cancellationToken);
    }

    public interface IAgentProviderFactory
    {
        public Task<string> Create(AgentInfo info, CancellationToken cancellationToken);
    }
}
