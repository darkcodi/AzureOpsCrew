namespace Worker.Models.Content;

public class AocMcpServerToolCallContent : AocAiContent
{
    public string? CallId { get; set; }
    public string? ToolName { get; set; }
    public string? ServerName { get; set; }
    public IReadOnlyDictionary<string, object?>? Arguments { get; set; }
}
