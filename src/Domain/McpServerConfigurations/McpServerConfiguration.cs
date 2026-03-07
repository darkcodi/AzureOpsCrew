#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.McpServerConfigurations;

public sealed class McpServerConfiguration
{
    private McpServerConfiguration()
    {
    }

    public McpServerConfiguration(Guid id, string name, string url)
    {
        Id = id;
        Name = name.Trim();
        Url = url.Trim();
        IsEnabled = true;
        Auth = new McpServerConfigurationAuth(McpServerConfigurationAuthType.None);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; set; }

    public string Url { get; private set; }

    public bool IsEnabled { get; private set; }

    public McpServerConfigurationAuth Auth { get; private set; }

    public List<McpServerToolConfiguration> Tools { get; private set; } = [];

    public DateTime? ToolsSyncedAt { get; private set; }

    public DateTime DateCreated { get; private set; } = DateTime.UtcNow;

    public void Update(string name, string url)
    {
        var normalizedUrl = url.Trim();
        var urlChanged = !string.Equals(Url, normalizedUrl, StringComparison.Ordinal);

        Name = name.Trim();
        Url = normalizedUrl;

        if (urlChanged)
        {
            Tools = [];
            ToolsSyncedAt = null;
        }
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    public void SetAuth(McpServerConfigurationAuth auth)
    {
        Auth = auth;
    }

    public void SetToolIsEnabled(string toolName, bool isEnabled)
    {
        var normalizedToolName = toolName.Trim();
        var tool = Tools.Single(tool => string.Equals(tool.Name, normalizedToolName, StringComparison.Ordinal));
        tool.IsEnabled = isEnabled;
    }

    public void ReplaceTools(IEnumerable<McpServerToolConfiguration> tools, DateTime syncedAt)
    {
        Tools = tools.ToList();
        ToolsSyncedAt = syncedAt;
    }
}
