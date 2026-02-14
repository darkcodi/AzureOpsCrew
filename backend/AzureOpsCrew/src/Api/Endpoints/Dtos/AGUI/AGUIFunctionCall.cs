using System.Text.Json.Serialization;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

public class AGUIFunctionCall
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}
