using Microsoft.Agents.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices
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
                "InMemory" => _serviceProvider.GetRequiredService<InMemoryAIContextProvider>(),
                _ => throw new InvalidOperationException($"{nameof(AIContextProvider)} of type {_type} is not defined.")
            };
        }
    }

    public class InMemoryAIContextProvider : AIContextProvider
    {
        protected override ValueTask<AIContext> InvokingCoreAsync(InvokingContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
        
    }
}
