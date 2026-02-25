namespace Worker.Models.Content;

public sealed class AocHostedFileContent : AocAiContent
{
    public string FileId { get; set; } = string.Empty;
    public string? MediaType { get; set; }
    public string? Name { get; set; }
}
