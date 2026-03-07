#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Agents
{
    public class Agent
    {
        private Agent()
        {
        }

        public Agent(Guid id, AgentInfo info, Guid providerId, string providerAgentId, string color)
        {
            Id = id;
            Info = info;
            ProviderId = providerId;
            ProviderAgentId = providerAgentId;
            Color = color;
        }

        public Guid Id { get; private set; }

        public string ProviderAgentId { get; private set; }

        public AgentInfo Info { get; private set; }

        public Guid ProviderId { get; private set; }

        public string Color { get; private set; } = "#43b581";

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public void Update(AgentInfo info, Guid providerId, string color)
        {
            Info = info;
            ProviderId = providerId;
            Color = color ?? "#43b581";
        }

        // Adds or replaces MCP server tool availability for this agent.
        // If a binding for the same McpServerConfigurationId already exists, it is replaced in-place.
        public void SetAvailableMcpServer(AgentMcpServerToolAvailability availability)
        {
            var current = Info.AvailableMcpServerTools;
            var index = Array.FindIndex(current, x => x.McpServerConfigurationId == availability.McpServerConfigurationId);

            if (index >= 0)
            {
                var updated = current.ToArray();
                updated[index] = availability;
                Info = Info with { AvailableMcpServerTools = updated };
            }
            else
            {
                Info = Info with { AvailableMcpServerTools = [..current, availability] };
            }
        }

        public void RemoveAvailableMcpServer(Guid mcpServerConfigurationId)
        {
            var filtered = Info.AvailableMcpServerTools
                .Where(x => x.McpServerConfigurationId != mcpServerConfigurationId)
                .ToArray();

            Info = Info with { AvailableMcpServerTools = filtered };
        }
    }
}
