namespace Worker.Models.Content;

public sealed class AocCodeInterpreterToolCallContent : AocAiContent
{
    public string? CallId { get; set; }
    public List<AocAiContent> Inputs { get; set; } = new();
}
