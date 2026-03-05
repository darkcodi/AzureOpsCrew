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

            var chatName = data.Channel?.Name ?? data.DmChannel?.GetDmChannelName();

            string FormatAgent(Agent agent)
            {
                return $"""
=====
Agent username: {agent.Info.Username}
Agent description: {agent.Info.Description}
Agent prompt: {agent.Info.Prompt}
=====
""";
            }

            var chatParticipants = string.Join("\n", data.ParticipantAgents.Where(a => a != data.Agent).Select(FormatAgent));

            var prompt = $"""
System prompt:
You are one of agents in group chat: agents + human.

Your username is:
{data.Agent.Info.Username}

Your description is:
{data.Agent.Info.Description}

Your user prompt is:
{data.Agent.Info.Prompt}

You are in the chat:
{chatName}

Here is a full list of all other AI agents in the chat:
{chatParticipants}

Available backend tools:
{beTools}

Available frontend tools:
{feTools}

VERY IMPORTANT!
CHAT RULES:
1. Always respond in a way that is consistent with your agent description and prompt.
2. Remember that you are not the only agent in the chat. Be mindful of other agents' personalities, descriptions, and prompts when crafting your responses.
3. Do NOT try to respond to each message in the chat. Only respond when you have something valuable to contribute, whether it's a message or a tool call.
4. If you see that message is not relevant to you, it's okay to ignore it and not respond. Just return SKIPPED in that case, and system will understand that you intentionally chose not to respond to that message.
5. Remember that all agents run in parallel, so some agents can post in chat while you are thinking or calling tools. Do not assume that the chat history will remain the same between the time you read it and the time you respond.
6. You can use the tools declared above to help you accomplish your goals. Each tool has a name, a description of what it does, and a JSON schema for the parameters it accepts.

""";

            return prompt;
        }
    }
}
