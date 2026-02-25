namespace Worker.Models.Content;

public sealed class AocImageGenerationToolCallContent : AocAiContent
{
    public string? ImageId { get; set; }
}
