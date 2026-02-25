namespace Worker.Models.Content;

public class AocFunctionCallContent : AocAiContent
{
    public string? CallId { get; set; }
    public string Name { get; set; } = string.Empty;
    public IDictionary<string, object?>? Arguments { get; set; }
    public bool InformationalOnly { get; set; }
}
