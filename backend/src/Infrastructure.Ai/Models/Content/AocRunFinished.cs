using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocRunFinished : AocAiContent
{
    public Guid ThreadId { get; set; }
    public Guid RunId { get; set; }
    public JsonElement? Result { get; set; }
}
