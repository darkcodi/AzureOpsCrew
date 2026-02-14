using System.Text.Json.Serialization;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

public class AGUIToolCall
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public AGUIFunctionCall Function { get; set; } = new();
}
