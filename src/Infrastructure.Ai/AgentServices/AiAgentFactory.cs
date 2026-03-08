using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.AgentServices;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices
{
    public class AiAgentFactory : IAiAgentFactory
    {
        private readonly AgentAiContextProviderFactory _contextProviderFactory;
        private readonly PromptService _promptService;

        public AiAgentFactory(AgentAiContextProviderFactory contextProviderFactory, PromptService promptService)
        {
            _contextProviderFactory = contextProviderFactory;
            _promptService = promptService;
        }

        public AIAgent Create(IChatClient client, AgentRunData data)
        {
            var prompt = _promptService.PreparePrompt(data);

            var aiTools = data.Tools.Select(x => (AITool)x.ToAiFunctionDeclaration()).ToArray();

            var options = new ChatClientAgentOptions
            {
                Name = data.Agent.Info.Username,
                ChatOptions = new ChatOptions
                {
                    Instructions = prompt,
                    Tools = aiTools,
                    AdditionalProperties = null,
                },
                AIContextProviderFactory = (context, ct) => _contextProviderFactory.Create(data.Agent.Id, context, ct),
            };

            return client.AsAIAgent(options);
        }
    }
}
