#pragma warning disable CS8618

namespace AzureOpsCrew.Domain.Chats;

public sealed class ChatMessageItem
{
    private ChatMessageItem() { }

    public ChatMessageItem(Guid id, Guid chatId, string content, Guid senderId)
    {
        Id = id;
        ChatId = chatId;
        Content = content;
        SenderId = senderId;
    }

    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string Content { get; set; } = string.Empty;
    public Guid SenderId { get; set; }
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
}
