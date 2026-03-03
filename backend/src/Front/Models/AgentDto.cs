namespace Front.Models;

public class AgentDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Color { get; set; } = "#43b581";
    public string Status { get; set; } = "Idle";
    public bool IsTyping { get; set; }
}

public class AgentToolDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
