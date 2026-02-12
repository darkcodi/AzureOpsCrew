using AzureOpsCrew.Domain.AgentManagements;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Ai.AgentManagements.Microsoft;

namespace AzureOpsCrew.Infrastructure.Ai.AgentManagements
{
    public class AgentFactory : IAgentFactory
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly Dictionary<Provider, Type> AgentProviderTypes =
            new Dictionary<Provider, Type>()
            {
                { Provider.Local0, typeof(Local0AgentFactory) },
                { Provider.Local1, typeof(Local1AgentFactory) },
                { Provider.MicrosoftFoundry, typeof(MicrosoftFoundryAgentFactory) }

            };

        public AgentFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task<string> Create(Provider provider, AgentInfo info, CancellationToken cancellationToken)
        {
            if (!AgentProviderTypes.ContainsKey(provider))
                throw new ArgumentOutOfRangeException($"Provider {provider} is not supported.");

            var agent = (IAgentProviderFactory)(_serviceProvider.GetService(AgentProviderTypes[provider])
                ?? throw new InvalidOperationException($"Agent provider factory of type {AgentProviderTypes[provider]} is not registered"));

            return agent.Create(info, cancellationToken);
        }
    }
}
