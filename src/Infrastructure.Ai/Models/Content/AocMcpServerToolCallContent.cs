namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocMcpServerToolCallContent : AocAiContent
{
    public string CallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public IReadOnlyDictionary<string, object?>? Arguments { get; set; }
}
