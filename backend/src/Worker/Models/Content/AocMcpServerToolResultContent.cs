namespace Worker.Models.Content;

public sealed class AocMcpServerToolResultContent : AocAiContent
{
    public string CallId { get; set; } = string.Empty;
    public List<AocAiContent>? Output { get; set; }
}
