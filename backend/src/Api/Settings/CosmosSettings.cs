namespace AzureOpsCrew.Api.Settings;

public class CosmosSettings
{
    public string? AccountEndpoint { get; set; }
    public string? AccountKey { get; set; }
    public string? DatabaseName { get; set; }
    public bool DisableSslValidation { get; set; } = false;
    public string? ConnectionMode { get; set; }
}
