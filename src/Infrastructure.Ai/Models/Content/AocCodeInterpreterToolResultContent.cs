namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocCodeInterpreterToolResultContent : AocAiContent
{
    public string? CallId { get; set; }
    public List<AocAiContent> Outputs { get; set; } = new();
}
