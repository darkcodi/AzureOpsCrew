using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories
{
    public class AgentAIContextProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _type;

        public AgentAIContextProviderFactory(IServiceProvider serviceProvider, string type)
        {
            _serviceProvider = serviceProvider;
            _type = type;
        }

        public async ValueTask<AIContextProvider> Create(ChatClientAgentOptions.AIContextProviderFactoryContext context, Guid agentId,CancellationToken cancellationToken)
        {
            return _type switch
            {
                "InMemory" => new InMemoryFactsContextProvider(agentId.ToString("D"), _serviceProvider.GetRequiredService<InMemoryFactsStore>()),
                _ => throw new InvalidOperationException($"{nameof(AIContextProvider)} of type {_type} is not defined.")
            };
        }
    }
}
