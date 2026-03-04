namespace AzureOpsCrew.Api.Settings;

public record AnthropicSettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string DefaultModel { get; set; } = "claude-opus-4-6";
}
