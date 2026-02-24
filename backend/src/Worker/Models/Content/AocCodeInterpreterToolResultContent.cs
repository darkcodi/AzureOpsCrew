namespace Worker.Models.Content;

public class AocCodeInterpreterToolResultContent : AocAiContent
{
    public string? CallId { get; set; }
    public List<AocAiContent> Outputs { get; set; } = new();
}
