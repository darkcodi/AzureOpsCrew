namespace Worker.Models.Content;

public class AocRunStart : AocSystemContent
{
    public string ThreadId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
}
