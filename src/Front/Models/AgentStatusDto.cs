using System.Text.Json.Serialization;

namespace Front.Models;

/// <summary>
/// Response DTO for a single agent's status from GET agent-statuses API.
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
