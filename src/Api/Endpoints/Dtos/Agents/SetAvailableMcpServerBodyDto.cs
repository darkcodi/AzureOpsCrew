namespace AzureOpsCrew.Api.Endpoints.Dtos.Agents;

public sealed class SetAvailableMcpServerBodyDto
{
    public Guid McpServerConfigurationId { get; set; }
    public string[]? EnabledToolNames { get; set; }
    public string[]? ApprovalRequiredNames { get; set; }
}
