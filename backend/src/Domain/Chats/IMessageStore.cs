namespace AzureOpsCrew.Domain.Chats;

public interface IMessageStore
{
    Task Init();
    Task<AocChat> CreateChat();
    Task DeleteChat(Guid chatId);
    Task PostMessage(Guid chatId, AocMessage message);
    Task DeleteMessage(Guid chatId, Guid messageId);
    Task<int> GetMessageCount(Guid chatId);
    Task<AocMessage[]> GetAllMessagesAfterDatetime(Guid chatId, DateTime? since = null);
    Task<AocMessage?> GetFirstMessageAfterDatetime(Guid chatId, DateTime since);
    Task<AocMessage?> GetMessageById(Guid chatId, Guid id);

    public async Task<AocMessage?> WaitForNextMessage(Guid chatId, DateTime? since, int? timeoutInSeconds, CancellationToken ct)
    {
        var timeout = timeoutInSeconds ?? int.MaxValue;
        var sinceValue = since ?? DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            var message = await GetFirstMessageAfterDatetime(chatId, sinceValue);
            if (message != null)
            {
                return message;
            }
            if (timeout <= 0)
            {
                break;
            }
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            timeout--;
        }

        return null;
    }
}
