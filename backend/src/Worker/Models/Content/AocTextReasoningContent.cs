namespace Worker.Models.Content;

public class AocTextReasoningContent : AocAiContent
{
    public string Text { get; set; } = string.Empty;
    public string? ProtectedData { get; set; } = string.Empty;
}
