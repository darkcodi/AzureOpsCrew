namespace AzureOpsCrew.Api.Chat;

using AzureOpsCrew.Domain.Chats;

public interface IChatServerClient
{
    // Chat CRUD
    Task<List<ChatEntity>> GetChatsAsync(CancellationToken cancellationToken = default);
    Task<ChatEntity?> GetChatAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ChatEntity> CreateChatAsync(string title, CancellationToken cancellationToken = default);
    Task<ChatEntity?> UpdateChatAsync(Guid id, string title, CancellationToken cancellationToken = default);
    Task<bool> DeleteChatAsync(Guid id, CancellationToken cancellationToken = default);

    // Messages
    Task<List<ChatMessageEntity>> GetMessagesAsync(Guid chatId, CancellationToken cancellationToken = default);
    Task<ChatMessageEntity> CreateMessageAsync(Guid chatId, string content, CancellationToken cancellationToken = default);

    // Participants
    Task<bool> AddParticipantAsync(Guid chatId, Guid participantId, CancellationToken cancellationToken = default);
    Task<bool> RemoveParticipantAsync(Guid chatId, Guid participantId, CancellationToken cancellationToken = default);
}
