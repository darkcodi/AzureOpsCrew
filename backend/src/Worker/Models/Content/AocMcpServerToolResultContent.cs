namespace Worker.Models.Content;

public class AocMcpServerToolResultContent : AocAiContent
{
    public string? CallId { get; set; }
    public List<AocAiContent>? Output { get; set; }
}
