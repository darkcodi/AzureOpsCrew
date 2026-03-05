namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocUriContent : AocAiContent
{
    public Uri Uri { get; set; } = new Uri("http://example.com");
    public string MediaType { get; set; } = string.Empty;
}
