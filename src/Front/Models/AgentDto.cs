using System.Text.Json.Serialization;

namespace Front.Models;

public class AgentDto
{
    public Guid Id { get; set; }

    [JsonPropertyName("info")]
    public AgentInfoDto? Info { get; set; }

    public Guid ProviderId { get; set; }

    public string Username => Info?.Username ?? string.Empty;
    public string Prompt => Info?.Prompt ?? string.Empty;
    public string Model => Info?.Model ?? string.Empty;
    public string? Description => Info?.Description;
    public IReadOnlyList<AgentMcpServerToolAvailabilityDto> AvailableMcpServerTools => Info?.AvailableMcpServerTools ?? [];

    public string Color { get; set; } = "#43b581";
    public string Status { get; set; } = "Idle";
    public string? ErrorMessage { get; set; }
    public bool IsTyping { get; set; }
}

public class AgentInfoDto
{
    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("availableMcpServerTools")]
    public List<AgentMcpServerToolAvailabilityDto> AvailableMcpServerTools { get; set; } = [];
}

public class AgentMcpServerToolAvailabilityDto
{
    [JsonPropertyName("mcpServerConfigurationId")]
    public Guid McpServerConfigurationId { get; set; }

    [JsonPropertyName("enabledToolNames")]
    public List<string> EnabledToolNames { get; set; } = [];
}

public class AgentToolDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
