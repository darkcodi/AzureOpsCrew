namespace Front.Models;

public class ReasoningDto
{
    public string Text { get; set; } = string.Empty;
    public string? AgentName { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
