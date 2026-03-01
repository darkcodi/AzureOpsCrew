using System.Net.Http.Json;
using System.Text.Json;
using AzureOpsCrew.Domain.Chats;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Worker.Settings;

namespace Worker.Chat;

public sealed class ChatServerClient(
    HttpClient httpClient,
    IOptions<ChatServerSettings> settings,
    ILogger<ChatServerClient> logger) : IChatServerClient
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // Chat CRUD

    public async Task<List<ChatEntity>> GetChatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync("api/chat/chats", cancellationToken);
            await EnsureSuccessAsync(response);

            var chats = await response.Content.ReadFromJsonAsync<List<ChatEntity>>(_jsonOptions, cancellationToken);
            return chats ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting chats from ChatServer");
            throw;
        }
    }

    public async Task<ChatEntity?> GetChatAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/chat/chats/{id}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ChatEntity>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "Error getting chat {ChatId} from ChatServer", id);
            throw;
        }
    }

    public async Task<ChatEntity> CreateChatAsync(string title, Guid[]? participantIds = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = new { Title = title, ParticipantIds = participantIds };
            var response = await httpClient.PostAsJsonAsync("api/chat/chats", dto, _jsonOptions, cancellationToken);
            await EnsureSuccessAsync(response);

            var chat = await response.Content.ReadFromJsonAsync<ChatEntity>(_jsonOptions, cancellationToken);
            return chat ?? throw new InvalidOperationException("Failed to deserialize created chat");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating chat with title '{Title}' in ChatServer", title);
            throw;
        }
    }

    public async Task<ChatEntity?> UpdateChatAsync(Guid id, string title, CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = new { Title = title };
            var response = await httpClient.PutAsJsonAsync($"api/chat/chats/{id}", dto, _jsonOptions, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            await EnsureSuccessAsync(response);
            return await response.Content.ReadFromJsonAsync<ChatEntity>(_jsonOptions, cancellationToken);
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "Error updating chat {ChatId} in ChatServer", id);
            throw;
        }
    }

    public async Task<bool> DeleteChatAsync(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"api/chat/chats/{id}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            await EnsureSuccessAsync(response);
            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "Error deleting chat {ChatId} from ChatServer", id);
            throw;
        }
    }

    // Messages

    public async Task<List<ChatMessageEntity>> GetMessagesAsync(Guid chatId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync($"api/chat/chats/{chatId}/messages", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return [];
            }

            await EnsureSuccessAsync(response);
            var messages = await response.Content.ReadFromJsonAsync<List<ChatMessageEntity>>(_jsonOptions, cancellationToken);
            return messages ?? [];
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "Error getting messages for chat {ChatId} from ChatServer", chatId);
            throw;
        }
    }

    public async Task<ChatMessageEntity> CreateMessageAsync(Guid chatId, string content, Guid senderId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"api/chat/chats/{chatId}/messages?senderId={senderId}";
            var dto = new { Content = content };
            var response = await httpClient.PostAsJsonAsync(url, dto, _jsonOptions, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"Chat with id {chatId} not found");
            }

            await EnsureSuccessAsync(response);
            var message = await response.Content.ReadFromJsonAsync<ChatMessageEntity>(_jsonOptions, cancellationToken);
            return message ?? throw new InvalidOperationException("Failed to deserialize created message");
        }
        catch (Exception ex) when (ex is not InvalidOperationException and not HttpRequestException)
        {
            logger.LogError(ex, "Error creating message for chat {ChatId} in ChatServer", chatId);
            throw;
        }
    }

    // Participants

    public async Task<bool> AddParticipantAsync(Guid chatId, Guid participantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsync($"api/chat/chats/{chatId}/participants/{participantId}", null, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            await EnsureSuccessAsync(response);
            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "Error adding participant {ParticipantId} to chat {ChatId} in ChatServer", participantId, chatId);
            throw;
        }
    }

    public async Task<bool> RemoveParticipantAsync(Guid chatId, Guid participantId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.DeleteAsync($"api/chat/chats/{chatId}/participants/{participantId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            await EnsureSuccessAsync(response);
            return true;
        }
        catch (Exception ex) when (ex is not HttpRequestException)
        {
            logger.LogError(ex, "Error removing participant {ParticipantId} from chat {ChatId} in ChatServer", participantId, chatId);
            throw;
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"ChatServer request failed: {response.StatusCode}. Content: {content}");
        }
    }
}
