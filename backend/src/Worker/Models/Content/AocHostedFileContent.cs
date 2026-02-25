namespace Worker.Models.Content;

public class AocHostedFileContent : AocAiContent
{
    public string? FileId { get; set; }
    public string? MediaType { get; set; }
    public string? Name { get; set; }
}
