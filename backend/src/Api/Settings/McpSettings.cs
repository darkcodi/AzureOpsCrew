namespace AzureOpsCrew.Api.Settings;

public record McpSettings
{
    public McpServerSettings Azure { get; set; } = new();
    public McpServerSettings AzureDevOps { get; set; } = new();
    public McpServerSettings Platform { get; set; } = new();
    public McpServerSettings GitOps { get; set; } = new();
}

public record McpServerSettings
{
    public string ServerUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    
    /// <summary>Azure subscription ID for MCP tools (optional)</summary>
    public string SubscriptionId { get; set; } = string.Empty;
}
