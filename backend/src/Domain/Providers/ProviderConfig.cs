namespace AzureOpsCrew.Domain.Providers;

public sealed class ProviderConfig
{
    private ProviderConfig()
    {
    }

    public ProviderConfig(
        Guid id,
        int clientId,
        string name,
        ProviderType providerType,
        string apiKey,
        string? apiEndpoint = null,
        string? defaultModel = null)
    {
        Id = id;
        ClientId = clientId;
        Name = name;
        ProviderType = providerType;
        ApiKey = apiKey;
        ApiEndpoint = apiEndpoint;
        DefaultModel = defaultModel;
    }

    public Guid Id { get; private set; }
    public int ClientId { get; private set; }
    public string Name { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public string ApiKey { get; private set; }
    public string? ApiEndpoint { get; private set; }
    public string? DefaultModel { get; private set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }

    public void Update(string name, string apiKey, string? apiEndpoint, string? defaultModel)
    {
        Name = name;
        ApiKey = apiKey;
        ApiEndpoint = apiEndpoint;
        DefaultModel = defaultModel;
        DateModified = DateTime.UtcNow;
    }
}
