namespace Worker.Models.Content;

public class AocRunError : AocSystemContent
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
}
