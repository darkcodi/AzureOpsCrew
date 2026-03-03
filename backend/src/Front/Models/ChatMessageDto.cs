namespace Front.Models;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }
    public string? AuthorName { get; set; }
    public Guid? AgentId { get; set; }
    public Guid? UserId { get; set; }
    public Guid? ChannelId { get; set; }
    public Guid? DmId { get; set; }

    public bool IsAgentMessage => AgentId.HasValue;
    public bool IsOwnMessage { get; set; }
}
