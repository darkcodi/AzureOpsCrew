using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Tools;

namespace AzureOpsCrew.Domain.Agents;

public class AgentRunData
{
    public Agent Agent { get; set; } = null!;
    public Provider Provider { get; set; } = null!;

    // one of these will be null depending on whether the agent is running in a channel or a DM
    public Channel? Channel { get; set; }
    public DirectMessageChannel? DmChannel { get; set; }

    public List<Message> ChatMessages { get; set; } = null!;
    public List<AgentThought> LlmThoughts { get; set; } = null!;
    public List<ToolDeclaration> Tools { get; set; } = null!;

    // List of all other agents in the channel or DM, including the current agent
    public List<Agent> ParticipantAgents { get; set; } = null!;
}
