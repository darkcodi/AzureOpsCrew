using System.Text.Json.Serialization;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Channels;

/// <summary>
/// Response DTO for a single agent's status (channel or DM).
/// </summary>
public class AgentStatusDto
{
    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; }
}
