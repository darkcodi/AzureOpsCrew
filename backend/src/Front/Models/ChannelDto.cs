namespace Front.Models;

public class ChannelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ConversationId { get; set; }
    public string[] AgentIds { get; set; } = [];
    public DateTime DateCreated { get; set; }
    public List<AgentDto> Agents { get; set; } = [];
}
