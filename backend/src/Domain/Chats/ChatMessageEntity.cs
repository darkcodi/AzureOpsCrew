#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Chats;

public sealed class ChatMessageEntity
{
    private ChatMessageEntity() { }

    public ChatMessageEntity(Guid id, Guid chatId, string content, Guid senderId)
    {
        Id = id;
        ChatId = chatId;
        Content = content;
        SenderId = senderId;
    }

    public Guid Id { get; private set; }
    public Guid ChatId { get; private set; }
    public string Content { get; private set; }

    public Guid SenderId { get; private set; }

    public DateTime PostedAt { get; private set; } = DateTime.UtcNow;
}
