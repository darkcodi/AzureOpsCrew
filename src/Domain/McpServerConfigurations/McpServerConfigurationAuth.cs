#pragma warning disable CS8618
namespace AzureOpsCrew.Domain.McpServerConfigurations;

public sealed record McpServerConfigurationAuth
{
    private McpServerConfigurationAuth()
    {
    }
    
    public McpServerConfigurationAuth(
        McpServerConfigurationAuthType type,
        string? bearerToken = null,
        List<AuthHeader>? headers = null)
    {
        Type = type;
        BearerToken = bearerToken;
        Headers = headers ?? [];
    }

    public McpServerConfigurationAuthType Type { get; init; }
    public string? BearerToken { get; init; }
    public List<AuthHeader> Headers { get; init; }
}
