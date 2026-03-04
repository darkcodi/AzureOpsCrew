namespace AzureOpsCrew.Api.Settings;

public sealed class BrevoSettings
{
    public string ApiBaseUrl { get; set; } = "https://api.brevo.com";
    public string ApiKey { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = "azureopscrew@aoc-app.com";
    public string SenderName { get; set; } = "Azure Ops Crew";
}
