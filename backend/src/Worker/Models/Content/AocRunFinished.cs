using System.Text.Json;

namespace Worker.Models.Content;

public sealed class AocRunFinished : AocAiContent
{
    public string ThreadId { get; set; } = string.Empty;

    public string RunId { get; set; } = string.Empty;

    public JsonElement? Result { get; set; }
}
