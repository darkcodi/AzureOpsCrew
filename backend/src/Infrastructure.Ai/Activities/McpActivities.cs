using System.Text.Json;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Ai.Models;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using Temporalio.Activities;

namespace AzureOpsCrew.Infrastructure.Ai.Activities;

public class McpActivities
{
    private readonly IChatServerClient _chatServerClient;

    public McpActivities(IChatServerClient chatServerClient)
    {
        _chatServerClient = chatServerClient;
    }

    [Activity]
    public async Task<ToolCallResult> CallMcpAsync(AocFunctionCallContent call, Guid agentId)
    {
        // ToDo: Implement proper handling of MCP calls
        switch (call.Name)
        {
            case "getMyIp":
                return await GetMyIpAsync();
            case "read_chat_messages":
                return await ReadChatMessagesAsync(call);
            case "post_chat_message":
                return await PostChatMessageAsync(call, agentId);
            default:
                return new ToolCallResult($"Unknown function: {call.Name}", IsError: true);
        }
    }

    private static readonly HttpClient HttpClient = new();

    private async Task<ToolCallResult> GetMyIpAsync()
    {
        var response = await HttpClient.GetAsync("https://free.freeipapi.com/api/json/");
        if (!response.IsSuccessStatusCode)
        {
            return new ToolCallResult($"Failed to call showMyIp API: {response.StatusCode}", IsError: true);
        }

        var content = await response.Content.ReadAsStringAsync();
        return new ToolCallResult(content, IsError: false);
    }

    private async Task<ToolCallResult> ReadChatMessagesAsync(AocFunctionCallContent call)
    {
        if (!call.Arguments.TryGetValue("chatId", out var chatIdValue) ||
            chatIdValue is not JsonElement chatIdElement ||
            !Guid.TryParse(chatIdElement.GetString(), out var chatId))
        {
            return new ToolCallResult("Invalid or missing chatId parameter", IsError: true);
        }

        var messages = await _chatServerClient.GetMessagesAsync(chatId);

        var serializedMessages = messages.Select(m => new
        {
            id = m.Id.ToString(),
            chatId = m.ChatId.ToString(),
            content = m.Content,
            senderId = m.SenderId.ToString(),
            postedAt = m.PostedAt.ToString("o")
        });

        return new ToolCallResult(
            JsonSerializer.Serialize(serializedMessages),
            IsError: false);
    }

    private async Task<ToolCallResult> PostChatMessageAsync(AocFunctionCallContent call, Guid senderId)
    {
        if (!call.Arguments.TryGetValue("chatId", out var chatIdValue) ||
            chatIdValue is not JsonElement chatIdElement ||
            !Guid.TryParse(chatIdElement.GetString(), out var chatId))
        {
            return new ToolCallResult("Invalid or missing chatId parameter", IsError: true);
        }

        if (!call.Arguments.TryGetValue("content", out var contentValue) ||
            contentValue is not JsonElement contentElement)
        {
            return new ToolCallResult("Missing content parameter", IsError: true);
        }

        var content = contentElement.GetString() ?? string.Empty;

        var message = await _chatServerClient.CreateMessageAsync(chatId, content, senderId);

        var serializedMessage = new
        {
            id = message.Id.ToString(),
            chatId = message.ChatId.ToString(),
            content = message.Content,
            senderId = message.SenderId.ToString(),
            postedAt = message.PostedAt.ToString("o")
        };

        return new ToolCallResult(
            JsonSerializer.Serialize(serializedMessage),
            IsError: false);
    }
}
