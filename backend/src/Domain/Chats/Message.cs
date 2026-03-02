namespace AzureOpsCrew.Domain.Chats;

public class Message
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }

    // Sender: exactly one of AgentId or UserId should be set
    public Guid? AgentId { get; set; }
    public Guid? UserId { get; set; }

    // Destination: exactly one of ChannelId or DmId should be set
    public Guid? ChannelId { get; set; }
    public Guid? DmId { get; set; }
}
