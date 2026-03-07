using System.Text.Json;
using System.Text.Json.Serialization;

namespace Front.Models;

public class Provider
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("backendId")]
    public string? BackendId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("providerType")]
    public string? ProviderType { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "draft";

    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = "";

    [JsonPropertyName("hasApiKey")]
    public bool? HasApiKey { get; set; }

    [JsonPropertyName("baseUrl")]
    public string BaseUrl { get; set; } = "";

    [JsonPropertyName("defaultModel")]
    public string DefaultModel { get; set; } = "";

    [JsonPropertyName("timeout")]
    public int Timeout { get; set; } = 30;

    [JsonPropertyName("rateLimit")]
    public int RateLimit { get; set; } = 60;

    [JsonPropertyName("availableModels")]
    public List<string> AvailableModels { get; set; } = [];

    [JsonPropertyName("selectedModels")]
    public List<string>? SelectedModels { get; set; }

    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; }

    [JsonPropertyName("dateCreated")]
    public string? DateCreated { get; set; }

    public Provider Clone() => new()
    {
        Id = Id,
        BackendId = BackendId,
        Name = Name,
        ProviderType = ProviderType,
        Status = Status,
        ApiKey = ApiKey,
        HasApiKey = HasApiKey,
        BaseUrl = BaseUrl,
        DefaultModel = DefaultModel,
        Timeout = Timeout,
        RateLimit = RateLimit,
        AvailableModels = [..AvailableModels],
        SelectedModels = SelectedModels != null ? [..SelectedModels] : null,
        IsDefault = IsDefault,
        DateCreated = DateCreated,
    };
}

public class McpServerConfigurationItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("backendId")]
    public string? BackendId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    [JsonPropertyName("toolsSyncedAt")]
    public string? ToolsSyncedAt { get; set; }

    [JsonPropertyName("dateCreated")]
    public string? DateCreated { get; set; }

    [JsonPropertyName("auth")]
    public McpServerAuthSummary Auth { get; set; } = new();

    [JsonPropertyName("tools")]
    public List<McpServerToolItem> Tools { get; set; } = [];

    /// <summary>Editable header list for the form. Populated from Auth.CustomHeaderNames on load (values not returned by API).</summary>
    public List<McpServerAuthHeaderEditorItem> Headers { get; set; } = [];

    public McpServerConfigurationItem Clone() => new()
    {
        Id = Id,
        BackendId = BackendId,
        Name = Name,
        Description = Description,
        Url = Url,
        IsEnabled = IsEnabled,
        ToolsSyncedAt = ToolsSyncedAt,
        DateCreated = DateCreated,
        Auth = Auth.Clone(),
        Tools = Tools.Select(tool => tool.Clone()).ToList(),
        Headers = Headers.Select(x => x.Clone()).ToList(),
    };
}

public class McpServerToolItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("inputSchemaJson")]
    public string? InputSchemaJson { get; set; }

    [JsonPropertyName("outputSchemaJson")]
    public string? OutputSchemaJson { get; set; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; } = true;

    public McpServerToolItem Clone() => new()
    {
        Name = Name,
        Description = Description,
        InputSchemaJson = InputSchemaJson,
        OutputSchemaJson = OutputSchemaJson,
        IsEnabled = IsEnabled,
    };
}

public class McpServerAuthSummary
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "None";

    [JsonPropertyName("hasBearerToken")]
    public bool HasBearerToken { get; set; }

    [JsonPropertyName("hasCustomHeaders")]
    public bool HasCustomHeaders { get; set; }

    [JsonPropertyName("customHeaderNames")]
    public List<string> CustomHeaderNames { get; set; } = [];

    public McpServerAuthSummary Clone() => new()
    {
        Type = Type,
        HasBearerToken = HasBearerToken,
        HasCustomHeaders = HasCustomHeaders,
        CustomHeaderNames = [..CustomHeaderNames],
    };
}

public class McpServerAuthEditorState
{
    public string Type { get; set; } = "None";

    public string BearerToken { get; set; } = "";

    public List<McpServerAuthHeaderEditorItem> Headers { get; set; } = [];

    public McpServerAuthEditorState Clone() => new()
    {
        Type = Type,
        BearerToken = BearerToken,
        Headers = Headers.Select(x => x.Clone()).ToList(),
    };
}

public class McpServerAuthHeaderEditorItem
{
    public string Name { get; set; } = "";

    public string Value { get; set; } = "";

    public McpServerAuthHeaderEditorItem Clone() => new()
    {
        Name = Name,
        Value = Value,
    };
}

public class ProviderTestResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("latencyMs")]
    public int? LatencyMs { get; set; }

    [JsonPropertyName("checkedAt")]
    public string? CheckedAt { get; set; }

    [JsonPropertyName("quota")]
    public string? Quota { get; set; }

    [JsonPropertyName("availableModels")]
    public List<AvailableModel>? AvailableModels { get; set; }
}

public class AvailableModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class AccountConfig
{
    public string Username { get; set; } = "User";
}

public class AppearanceConfig
{
    public string Theme { get; set; } = "dark";
    public string FontSize { get; set; } = "medium";
    public bool CompactMode { get; set; }
}

public class NotificationConfig
{
    public bool DesktopNotifications { get; set; } = true;
    public bool SoundEnabled { get; set; } = true;
    public bool MentionNotifications { get; set; } = true;
}

public class RoutingConfig
{
    public string Strategy { get; set; } = "priority";
    public bool FallbackEnabled { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
}

public class AdvancedConfig
{
    public bool DebugMode { get; set; }
    public string LogLevel { get; set; } = "warn";
    public int RequestTimeout { get; set; } = 30;
    public int MaxConcurrentRequests { get; set; } = 5;
}

public class SettingsState
{
    public List<Provider> Providers { get; set; } = [];
    public AccountConfig Account { get; set; } = new();
    public AppearanceConfig Appearance { get; set; } = new();
    public NotificationConfig Notifications { get; set; } = new();
    public RoutingConfig Routing { get; set; } = new();
    public AdvancedConfig Advanced { get; set; } = new();

    public SettingsState DeepClone() => new()
    {
        Providers = Providers.Select(p => p.Clone()).ToList(),
        Account = new AccountConfig { Username = Account.Username },
        Appearance = new AppearanceConfig
        {
            Theme = Appearance.Theme,
            FontSize = Appearance.FontSize,
            CompactMode = Appearance.CompactMode,
        },
        Notifications = new NotificationConfig
        {
            DesktopNotifications = Notifications.DesktopNotifications,
            SoundEnabled = Notifications.SoundEnabled,
            MentionNotifications = Notifications.MentionNotifications,
        },
        Routing = new RoutingConfig
        {
            Strategy = Routing.Strategy,
            FallbackEnabled = Routing.FallbackEnabled,
            MaxRetries = Routing.MaxRetries,
        },
        Advanced = new AdvancedConfig
        {
            DebugMode = Advanced.DebugMode,
            LogLevel = Advanced.LogLevel,
            RequestTimeout = Advanced.RequestTimeout,
            MaxConcurrentRequests = Advanced.MaxConcurrentRequests,
        },
    };
}

public class SaveProvidersResponse
{
    [JsonPropertyName("providers")]
    public List<SavedProviderRef>? Providers { get; set; }
}

public class SavedProviderRef
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("backendId")]
    public string BackendId { get; set; } = "";
}

public static class DefaultSettings
{
    public static SettingsState Create() => new()
    {
        Providers =
        [
            new Provider
            {
                Id = "openai", Name = "OpenAI", ProviderType = "OpenAI",
                Status = "enabled", ApiKey = "", BaseUrl = "https://api.openai.com/v1",
                DefaultModel = "", Timeout = 30, RateLimit = 60, IsDefault = true,
            },
            new Provider
            {
                Id = "azure-foundry", Name = "Azure Foundry", ProviderType = "AzureFoundry",
                Status = "disabled", ApiKey = "", BaseUrl = "https://your-resource.openai.azure.com",
                DefaultModel = "", Timeout = 30, RateLimit = 60,
            },
            new Provider
            {
                Id = "anthropic", Name = "Anthropic", ProviderType = "Anthropic",
                Status = "disabled", ApiKey = "", BaseUrl = "https://api.anthropic.com/v1",
                DefaultModel = "", Timeout = 30, RateLimit = 60,
            },
            new Provider
            {
                Id = "ollama", Name = "Ollama (Local)", ProviderType = "Ollama (Local)",
                Status = "enabled", ApiKey = "", BaseUrl = "http://localhost:11434",
                DefaultModel = "", Timeout = 120, RateLimit = 0,
            },
            new Provider
            {
                Id = "openrouter", Name = "OpenRouter", ProviderType = "OpenRouter",
                Status = "disabled", ApiKey = "", BaseUrl = "https://openrouter.ai/api/v1",
                DefaultModel = "", Timeout = 30, RateLimit = 60,
            },
        ],
        Account = new AccountConfig { Username = "User" },
        Appearance = new AppearanceConfig { Theme = "dark", FontSize = "medium" },
        Notifications = new NotificationConfig
        {
            DesktopNotifications = true, SoundEnabled = true, MentionNotifications = true,
        },
        Routing = new RoutingConfig { Strategy = "priority", FallbackEnabled = true, MaxRetries = 3 },
        Advanced = new AdvancedConfig
        {
            LogLevel = "warn", RequestTimeout = 30, MaxConcurrentRequests = 5,
        },
    };
}

public enum SettingsSection
{
    Account,
    Appearance,
    Notifications,
    Providers,
    McpServers,
    Agents,
    Routing,
    Advanced,
}

public static class SettingsSectionRoutes
{
    private static readonly Dictionary<string, SettingsSection> SlugToSection = new(StringComparer.OrdinalIgnoreCase)
    {
        ["account"] = SettingsSection.Account,
        ["appearance"] = SettingsSection.Appearance,
        ["notifications"] = SettingsSection.Notifications,
        ["providers"] = SettingsSection.Providers,
        ["mcp-servers"] = SettingsSection.McpServers,
        ["agents"] = SettingsSection.Agents,
        ["routing"] = SettingsSection.Routing,
        ["advanced"] = SettingsSection.Advanced,
    };

    private static readonly Dictionary<SettingsSection, string> SectionToSlug = new()
    {
        [SettingsSection.Account] = "account",
        [SettingsSection.Appearance] = "appearance",
        [SettingsSection.Notifications] = "notifications",
        [SettingsSection.Providers] = "providers",
        [SettingsSection.McpServers] = "mcp-servers",
        [SettingsSection.Agents] = "agents",
        [SettingsSection.Routing] = "routing",
        [SettingsSection.Advanced] = "advanced",
    };

    public static SettingsSection FromSlug(string? slug) =>
        !string.IsNullOrEmpty(slug) && SlugToSection.TryGetValue(slug, out var section)
            ? section
            : SettingsSection.Providers;

    public static string ToSlug(SettingsSection section) =>
        SectionToSlug.GetValueOrDefault(section, "providers");

    public static string ToPath(SettingsSection section) =>
        $"/settings/{ToSlug(section)}";
}

/// <summary>
/// Matches the raw JSON shape returned by the backend GET /api/providers endpoint.
/// Field names differ from the frontend <see cref="Provider"/> model.
/// </summary>
public class BackendProviderDto
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("providerType")] public JsonElement ProviderType { get; set; }
    [JsonPropertyName("hasApiKey")] public bool HasApiKey { get; set; }
    [JsonPropertyName("apiEndpoint")] public string? ApiEndpoint { get; set; }
    [JsonPropertyName("defaultModel")] public string? DefaultModel { get; set; }
    [JsonPropertyName("selectedModels")] public string? SelectedModels { get; set; }
    [JsonPropertyName("isEnabled")] public bool IsEnabled { get; set; }
    [JsonPropertyName("modelsCount")] public int ModelsCount { get; set; }
    [JsonPropertyName("dateCreated")] public string? DateCreated { get; set; }
}

/// <summary>
/// Maps between the backend's numeric provider type codes and the frontend string names.
/// </summary>
public static class ProviderTypeMap
{
    private static readonly Dictionary<int, string> NumberToName = new()
    {
        [100] = "OpenAI",
        [200] = "Anthropic",
        [300] = "Ollama (Local)",
        [400] = "OpenRouter",
        [500] = "AzureFoundry",
    };

    private static readonly Dictionary<string, int> NameToNumber = new(StringComparer.OrdinalIgnoreCase)
    {
        ["OpenAI"] = 100,
        ["Azure OpenAI"] = 100,
        ["Anthropic"] = 200,
        ["Ollama (Local)"] = 300,
        ["Ollama"] = 300,
        ["OpenRouter"] = 400,
        ["AzureFoundry"] = 500,
        ["Azure Foundry"] = 500,
    };

    public static string FromBackend(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out var num))
            return NumberToName.GetValueOrDefault(num, "OpenAI");
        if (element.ValueKind == JsonValueKind.String)
            return element.GetString() ?? "OpenAI";
        return "OpenAI";
    }

    public static int ToBackend(string typeName) =>
        NameToNumber.GetValueOrDefault(typeName, 100);

    public static List<string> SafeParseSelectedModels(string? value)
    {
        if (string.IsNullOrEmpty(value)) return [];
        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(value);
            return parsed ?? [];
        }
        catch
        {
            return [];
        }
    }
}
