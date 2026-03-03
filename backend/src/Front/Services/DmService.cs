using System.Net.Http.Json;
using Front.Models;
using Serilog;

namespace Front.Services;

public class DmService
{
    private readonly HttpClient _httpClient;

    public DmService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<DmChannelDto>> GetDmsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<DmChannelDto>>("/api/dms");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching DMs: {ex.Message}");
            return [];
        }
    }

    public async Task<List<ChatMessageDto>> GetDmMessagesAsync(Guid dmId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ChatMessageDto>>($"/api/dms/{dmId}/messages");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching messages for DM {dmId}: {ex.Message}");
            return [];
        }
    }

    public async Task<List<ChatMessageDto>> GetUserDmMessagesAsync(Guid otherUserId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ChatMessageDto>>($"/api/dms/users/{otherUserId}/messages");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching messages for user DM {otherUserId}: {ex.Message}");
            return [];
        }
    }

    public async Task<List<ChatMessageDto>> GetAgentDmMessagesAsync(Guid agentId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ChatMessageDto>>($"/api/dms/agents/{agentId}/messages");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching messages for agent DM {agentId}: {ex.Message}");
            return [];
        }
    }

    public async Task<ChatMessageDto?> PostUserDmMessageAsync(Guid otherUserId, string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/dms/users/{otherUserId}/messages",
                new { Content = text });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ChatMessageDto>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error posting user DM message: {ex.Message}");
            return null;
        }
    }

    public async Task<ChatMessageDto?> PostAgentDmMessageAsync(Guid agentId, string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/dms/agents/{agentId}/messages",
                new { Content = text });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ChatMessageDto>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error posting agent DM message: {ex.Message}");
            return null;
        }
    }
}
