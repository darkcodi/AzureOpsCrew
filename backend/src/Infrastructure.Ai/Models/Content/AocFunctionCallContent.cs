namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocFunctionCallContent : AocAiContent
{
    public string CallId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public IDictionary<string, object?>? Arguments { get; set; }
    public bool InformationalOnly { get; set; }
}
