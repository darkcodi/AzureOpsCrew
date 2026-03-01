#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Chats;

public sealed class ChatMessageEntity
{
    private ChatMessageEntity() { }

    public ChatMessageEntity(Guid id, Guid chatId, string content)
    {
        Id = id;
        ChatId = chatId;
        Content = content;
    }

    public Guid Id { get; private set; }
    public Guid ChatId { get; private set; }
    public string Content { get; private set; }

    // Sender: exactly one of SenderUserId or SenderAgentId should be set
    public Guid? SenderUserId { get; private set; }
    public Guid? SenderAgentId { get; private set; }

    public DateTime PostedAt { get; private set; } = DateTime.UtcNow;

    public void SetSenderUser(Guid userId)
    {
        SenderUserId = userId;
        SenderAgentId = null;
    }

    public void SetSenderAgent(Guid agentId)
    {
        SenderAgentId = agentId;
        SenderUserId = null;
    }
}
