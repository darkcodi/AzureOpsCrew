namespace AzureOpsCrew.Api.Settings;

public record OpenAISettings
{
    public string ApiKey { get; set; } = string.Empty;
}
