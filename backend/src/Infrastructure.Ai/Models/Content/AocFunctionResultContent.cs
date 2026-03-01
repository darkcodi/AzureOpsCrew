namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocFunctionResultContent : AocAiContent
{
    public string CallId { get; set; } = string.Empty;
    public object? Result { get; set; }
}
