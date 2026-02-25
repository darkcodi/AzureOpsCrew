namespace Worker.Models.Content;

public class AocErrorContent : AocAiContent
{
    public string? Message { get; set; }
    public string? ErrorCode { get; set; }
    public string? Details { get; set; }
}
