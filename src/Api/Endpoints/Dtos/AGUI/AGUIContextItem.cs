using System.Text.Json.Serialization;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

public class AGUIContextItem
{
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
