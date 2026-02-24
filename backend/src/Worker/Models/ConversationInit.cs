namespace Worker.Models;

public class ConversationInit
{
    public string ThreadId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public Guid AgentId { get; set; }
}
