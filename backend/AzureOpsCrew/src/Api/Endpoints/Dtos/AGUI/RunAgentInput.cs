using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

public class RunAgentInput
{
    [JsonPropertyName("threadId")]
    public string ThreadId { get; set; } = string.Empty;

    [JsonPropertyName("runId")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement State { get; set; }

    [JsonPropertyName("messages")]
    public IEnumerable<AGUIMessage> Messages { get; set; } = [];

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public IEnumerable<AGUITool>? Tools { get; set; }

    [JsonPropertyName("context")]
    public AGUIContextItem[] Context { get; set; } = [];

    [JsonPropertyName("forwardedProps")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public JsonElement ForwardedProperties { get; set; }
}
