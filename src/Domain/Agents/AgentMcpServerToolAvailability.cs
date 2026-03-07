namespace AzureOpsCrew.Domain.Agents;

// Defines which tools from a specific MCP server are available to an agent.
// If EnabledToolNames is empty, no tools from this server are available.
public record AgentMcpServerToolAvailability(Guid McpServerConfigurationId)
{
    public string[] EnabledToolNames { get; set; } = [];
}
