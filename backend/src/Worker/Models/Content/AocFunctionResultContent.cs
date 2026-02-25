namespace Worker.Models.Content;

public class AocFunctionResultContent : AocAiContent
{
    public string? CallId { get; set; }
    public object? Result { get; set; }
}
