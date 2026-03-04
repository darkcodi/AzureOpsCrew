namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocRunError : AocAiContent
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
}
