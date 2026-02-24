namespace AzureOpsCrew.Domain.Chats;

public class AocMessage
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }

    // Sender: exactly one of AgentId or UserId should be set
    public string? AgentId { get; set; }
    public string? UserId { get; set; }

    // Destination: exactly one of ChannelId or DmId should be set
    public Guid? ChannelId { get; set; }
    public Guid? DmId { get; set; }

    public AocChat Chat { get; set; } = null!;
}
