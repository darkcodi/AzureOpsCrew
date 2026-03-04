namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocDataContent : AocAiContent
{
    public string? MediaType { get; set; }
    public byte[]? Data { get; set; }
    public string Uri { get; set; } = string.Empty;
}
