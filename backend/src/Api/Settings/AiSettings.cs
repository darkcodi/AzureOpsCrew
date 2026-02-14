namespace AzureOpsCrew.Api.Settings;

public class AiSettings
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }

    public bool IsValid()
    {
        return !string.IsNullOrEmpty(Endpoint) && !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(Model);
    }
}
