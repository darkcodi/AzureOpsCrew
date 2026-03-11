using System.Text;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.McpServerConfigurations;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class ChatParticipantsPromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data) => true;

    public string GetContent(AgentRunData data)
    {
        var isChannel = data.Channel != null;
        var isDm = data.DmChannel != null;

        if (isChannel)
        {
            return ChannelParticipants(data);
        }

        if (isDm)
        {
            return DmParticipants(data);
        }

        return string.Empty;
    }

    private string ChannelParticipants(AgentRunData data)
    {
        var channel = data.Channel!;
        var chatParticipants = string.Join("\n", data.ParticipantAgents.Where(x => x.Id != data.Agent.Id).Select(x => FormatAgent(x, data.AllMcpServers)));

        return $"""
## Chat participants information
You are in the channel: {channel.Name}

Here is a full list of all other AI agents in this channel:
{chatParticipants}

""";
    }

    private string DmParticipants(AgentRunData data)
    {
        // ToDo: Add user info to data
        var username = "<hidden>"; // user.Username

        return $"""
## Chat participants information
You are in a DM chat with the user: {username}

""";
    }

    private string FormatAgent(Agent agent, List<McpServerConfiguration> mcpServers)
    {
        return $"""
=====
Agent username: {agent.Info.Username}
Agent description: {agent.Info.Description}
Agent MCP servers/tools:
{FormatTools(agent.Info.AvailableMcpServerTools, mcpServers)}
=====
""";
    }

    private string FormatTools(AgentMcpServerToolAvailability[] tools, List<McpServerConfiguration> mcpServers)
    {
        // Format:
        // - Server1: tool1, tool2
        // - Server2: tool3, tool4
        // - <unknown server>: tool5, tool6 (if server config is missing)

        var sb = new StringBuilder();

        foreach (var mcpServerToolAvailability in tools)
        {
            var serverName = mcpServers.FirstOrDefault(s => s.Id == mcpServerToolAvailability.McpServerConfigurationId)?.Name ?? "<unknown server>";
            var toolList = string.Join(", ", mcpServerToolAvailability.EnabledToolNames);
            sb.AppendLine($"- {serverName}: {toolList}");
        }

        return sb.ToString();
    }
}
