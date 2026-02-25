namespace Worker.Models.Content;

public sealed class AocRunStart : AocAiContent
{
    public Guid ThreadId { get; set; }
    public Guid RunId { get; set; }
}
