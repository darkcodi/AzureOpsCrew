using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.McpServerConfigurations;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.Agents;

public class AgentRunData
{
    public Agent Agent { get; set; } = null!;
    public Provider Provider { get; set; } = null!;

    // one of these will be null depending on whether the agent is running in a channel or a DM
    public Channel? Channel { get; set; }
    public DirectMessageChannel? DmChannel { get; set; }

    public List<Message> ChatMessages { get; set; } = null!;
    public List<ChatMessage> ChatMessagesMapped { get; set; } = null!;
    public List<AgentThought> LlmThoughts { get; set; } = null!;
    public List<ChatMessage> LlmThoughtsMapped { get; set; } = null!;
    public List<ToolDeclaration> Tools { get; set; } = null!;

    // MCP server configurations whose tools are available to the agent.
    // Used to resolve server connection details when executing MCP tool calls.
    public List<McpServerConfiguration> McpServers { get; set; } = [];

    // List of all other agents in the channel or DM, including the current agent
    public List<Agent> ParticipantAgents { get; set; } = null!;
}
