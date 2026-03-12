namespace AzureOpsCrew.Domain.Agents;

// Defines which tools from a specific MCP server are available to an agent.
// If EnabledToolNames is empty, no tools from this server are available.
// TODO: Merge EnabledToolNames and ApprovalRequiredNames into a single EnabledTool object
// with properties like Name, RequiresApproval, etc.
public record AgentMcpServerToolAvailability(Guid McpServerConfigurationId)
{
    public string[] EnabledToolNames { get; set; } = [];
    public string[] ApprovalRequiredNames { get; set; } = [];
}
