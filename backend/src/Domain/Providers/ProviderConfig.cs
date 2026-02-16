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
        string? apiKey,
        string? apiEndpoint = null,
        string? defaultModel = null,
        bool isEnabled = true,
        string? selectedModels = null)
    {
        Id = id;
        ClientId = clientId;
        Name = name;
        ProviderType = providerType;
        ApiKey = apiKey;
        ApiEndpoint = apiEndpoint;
        DefaultModel = defaultModel;
        IsEnabled = isEnabled;
        SelectedModels = selectedModels;
    }

    public Guid Id { get; private set; }
    public int ClientId { get; private set; }
    public string Name { get; private set; }
    public ProviderType ProviderType { get; private set; }
    public string? ApiKey { get; private set; }
    public string? ApiEndpoint { get; private set; }
    public string? DefaultModel { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? SelectedModels { get; private set; }
    public int ModelsCount { get; private set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime? DateModified { get; private set; }

    public void Update(string name, string? apiKey, string? apiEndpoint, string? defaultModel, bool? isEnabled = null, string? selectedModels = null)
    {
        Name = name;
        ApiKey = apiKey;
        ApiEndpoint = apiEndpoint;
        DefaultModel = defaultModel;
        SelectedModels = selectedModels;
        if (isEnabled.HasValue)
            IsEnabled = isEnabled.Value;
        DateModified = DateTime.UtcNow;
    }

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        DateModified = DateTime.UtcNow;
    }

    public void SetModelsCount(int count)
    {
        ModelsCount = count;
    }
}
