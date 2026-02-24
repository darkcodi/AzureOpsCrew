using AzureOpsCrew.Domain.Chats;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Infrastructure.Db;

public class EfContextMessageStore : IMessageStore
{
    private readonly AzureOpsCrewContext _context;

    public EfContextMessageStore(AzureOpsCrewContext context)
    {
        _context = context;
    }

    public Task Init()
    {
        // No initialization needed for EF Core with SqlServer
        return Task.CompletedTask;
    }

    public async Task<AocChat> CreateChat()
    {
        var newChat = new AocChat
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        };
        _context.Chats.Add(newChat);
        await _context.SaveChangesAsync();
        return newChat;
    }

    public async Task DeleteChat(Guid chatId)
    {
        var chat = await _context.Chats.FindAsync(chatId);
        if (chat != null)
        {
            _context.Chats.Remove(chat);
            await _context.SaveChangesAsync();
        }
    }

    public async Task PostMessage(Guid chatId, AocMessage message)
    {
        var chat = await _context.Chats.FindAsync(chatId);
        if (chat == null)
        {
            throw new InvalidOperationException($"Chat with ID {chatId} not found.");
        }

        message.Id = Guid.NewGuid();
        message.ChatId = chatId;
        message.PostedAt = DateTime.UtcNow;

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteMessage(Guid chatId, Guid messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message != null && message.ChatId == chatId)
        {
            _context.Messages.Remove(message);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<int> GetMessageCount(Guid chatId)
    {
        return await _context.Messages.CountAsync(m => m.ChatId == chatId);
    }

    public async Task<AocMessage[]> GetAllMessagesAfterDatetime(Guid chatId, DateTime? since = null)
    {
        var query = _context.Messages.Where(m => m.ChatId == chatId);
        if (since.HasValue)
        {
            query = query.Where(m => m.PostedAt > since.Value);
        }
        return await query.OrderBy(m => m.PostedAt).ToArrayAsync();
    }

    public async Task<AocMessage?> GetFirstMessageAfterDatetime(Guid chatId, DateTime since)
    {
        return await _context.Messages
            .Where(m => m.ChatId == chatId && m.PostedAt > since)
            .OrderBy(m => m.PostedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<AocMessage?> GetMessageById(Guid chatId, Guid id)
    {
        return await _context.Messages
            .Where(m => m.ChatId == chatId && m.Id == id)
            .FirstOrDefaultAsync();
    }
}
