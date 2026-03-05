namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;

public record SetMcpServerConfigurationEnabledBodyDto
{
    public bool IsEnabled { get; set; }
}
