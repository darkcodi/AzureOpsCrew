namespace AzureOpsCrew.Domain.McpServerConfigurations;

public record McpServerToolConfiguration(string Name)
{
    public bool IsEnabled { get; set; } = true;

    public string? Description { get; set; }
}
