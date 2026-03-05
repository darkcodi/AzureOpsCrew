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
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; set; }

    public string Url { get; private set; }

    public bool IsEnabled { get; private set; }

    public List<McpServerToolConfiguration> Tools { get; private set; } = [];

    public DateTime DateCreated { get; private set; } = DateTime.UtcNow;

    public void Update(string name, string url)
    {
        Name = name.Trim();
        Url = url.Trim();
    }

    public void SetEnabled(bool isEnabled)
    {
        IsEnabled = isEnabled;
    }

    public void ReplaceTools(IEnumerable<McpServerToolConfiguration> tools)
    {
        Tools = tools.ToList();
    }
}
