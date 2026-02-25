namespace Worker.Models.Content;

public class AocDataContent : AocAiContent
{
    public string? MediaType { get; set; }
    public byte[]? Data { get; set; }
    public string? Uri { get; set; }
}
