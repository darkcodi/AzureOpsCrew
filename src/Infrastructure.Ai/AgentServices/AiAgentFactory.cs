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

        public AiAgentFactory(AgentAiContextProviderFactory contextProviderFactory)
        {
            _contextProviderFactory = contextProviderFactory;
        }

        public AIAgent Create(IChatClient client, AgentRunData data)
        {
            var prompt = PreparePrompt(data);

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

        private string PreparePrompt(AgentRunData data)
        {
            const string systemPrompt = "You are one of agents in group chat: agents + human. When you have tools available, use them proactively to present information visually instead of plain text. Do NOT issue several tool calls in a row, and always wait for the result of a tool call before issuing another tool call. If you want to issue multiple tool calls, please issue them one by one and wait for the result of each tool call.";

            var beTools = string.Join("\n\n", data.Tools.Where(x => x.ToolType == ToolType.BackEnd).Select(t => t.FormatToolDeclaration()));
            var feTools = string.Join("\n\n", data.Tools.Where(x => x.ToolType == ToolType.FrontEnd).Select(t => t.FormatToolDeclaration()));

            if (string.IsNullOrEmpty(beTools))
            {
                beTools = "No backend tools available.";
            }
            if (string.IsNullOrEmpty(feTools))
            {
                feTools = "No frontend tools available.";
            }

            var prompt = $"""
System prompt:
{systemPrompt}

Available backend tools:
{beTools}

Available frontend tools:
{feTools}

Your username is:
{data.Agent.Info.Username}

Your description is:
{data.Agent.Info.Description}

User prompt:
{data.Agent.Info.Prompt}
""";

            return prompt;
        }
    }
}
