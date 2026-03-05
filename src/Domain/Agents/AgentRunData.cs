using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.Tools;

namespace AzureOpsCrew.Domain.Agents;

public class AgentRunData
{
    public Agent Agent { get; set; } = null!;
    public Provider Provider { get; set; } = null!;
    public Channel? Channel { get; set; }
    public DirectMessageChannel? Dm { get; set; }
    public List<Message> ChatMessages { get; set; } = null!;
    public List<AgentThought> LlmThoughts { get; set; } = null!;
    public List<ToolDeclaration> Tools { get; set; } = null!;
}
