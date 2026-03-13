using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.Cypher;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.InMemory;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.None;
using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories
{
    public class AgentAiContextProviderFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly string _type;

        public AgentAiContextProviderFactory(IServiceProvider serviceProvider, string type)
        {
            _serviceProvider = serviceProvider;
            _type = type;
        }

        public async ValueTask<AIContextProvider> Create(
            Guid agentId,
            ChatClientAgentOptions.AIContextProviderFactoryContext context,
            CancellationToken ct)
        {
            return _type switch
            {
                "None" => new NoneContextProvider(),
                "InMemory" => new InMemoryFactsContextProvider(agentId, _serviceProvider.GetRequiredService<InMemoryFactsStore>()),
                "Neo4j" => new CypherFactsContextProvider(agentId, _serviceProvider.GetRequiredService<CypherFactsStore>()),
                _ => throw new InvalidOperationException($"{nameof(AIContextProvider)} of type {_type} is not defined.")
            };
        }
    }
}
