namespace AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;
public record McpServerConfigurationAuthResponseDto
{
    public string Type { get; init; } = string.Empty;
    public bool HasBearerToken { get; init; }
    public bool HasCustomHeaders { get; init; }
    public string[] CustomHeaderNames { get; init; } = [];
}
