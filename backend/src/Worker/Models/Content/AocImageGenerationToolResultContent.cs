namespace Worker.Models.Content;

public sealed class AocImageGenerationToolResultContent : AocAiContent
{
    public string? ImageId { get; set; }
    public List<AocAiContent>? Outputs { get; set; }
}
