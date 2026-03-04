using System.Text.Json.Serialization;

namespace Front.Models;

public class AgentDto
{
    public Guid Id { get; set; }

    [JsonPropertyName("info")]
    public AgentInfoDto? Info { get; set; }

    public string Username => Info?.Username ?? string.Empty;
    public string Prompt => Info?.Prompt ?? string.Empty;
    public string Model => Info?.Model ?? string.Empty;
    public string? Description => Info?.Description;

    public string Color { get; set; } = "#43b581";
    public string Status { get; set; } = "Idle";
    public bool IsTyping { get; set; }
}

public class AgentInfoDto
{
    public string Username { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class AgentToolDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
