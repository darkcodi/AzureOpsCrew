namespace Worker.Models.Content;

public sealed class AocRunStart : AocAiContent
{
    public string ThreadId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
}
