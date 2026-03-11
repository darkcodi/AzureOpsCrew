using System.Net.Http.Json;
using Front.Models;
using Serilog;

namespace Front.Services;

public class ChannelService
{
    private readonly HttpClient _httpClient;

    public ChannelService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<ChannelDto>> GetChannelsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ChannelDto>>("/api/channels");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching channels: {ex.Message}");
            return [];
        }
    }

    public async Task<ChannelDto?> GetChannelAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<ChannelDto>($"/api/channels/{id}");
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching channel {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<List<ChatMessageDto>> GetChannelMessagesAsync(Guid channelId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ChatMessageDto>>($"/api/channels/{channelId}/messages");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching messages for channel {channelId}: {ex.Message}");
            return [];
        }
    }

    public async Task<ChatMessageDto?> PostMessageAsync(Guid channelId, string text)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/channels/{channelId}/messages",
                new { Content = text });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ChatMessageDto>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error posting message to channel {channelId}: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> CreateChannelAsync(string name, string? description, Guid[] agentIds)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/channels/create",
                new { Name = name, Description = description, AgentIds = agentIds });

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error($"Error creating channel: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> AddAgentToChannelAsync(Guid channelId, Guid agentId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/channels/{channelId}/add-agent",
                new { AgentId = agentId });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error($"Error adding agent to channel: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> RemoveAgentFromChannelAsync(Guid channelId, Guid agentId)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/channels/{channelId}/remove-agent",
                new { AgentId = agentId });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error($"Error removing agent from channel: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteChannelAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/channels/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error($"Error deleting channel {id}: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ApprovalRequestDto>> GetApprovalsAsync(Guid channelId)
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<ApprovalRequestDto>>($"/api/channels/{channelId}/approvals");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching approvals for channel {channelId}: {ex.Message}");
            return [];
        }
    }

    public async Task<bool> RespondToApprovalAsync(Guid channelId, string approvalId, bool approved, string? reason = null)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/channels/{channelId}/approvals/{approvalId}",
                new { Approved = approved, Reason = reason });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error($"Error responding to approval {approvalId}: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> StopAgentChannelAsync(Guid channelId, Guid agentId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/api/channels/{channelId}/agents/{agentId}/stop", null);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error($"Error stopping agent {agentId} in channel {channelId}: {ex.Message}");
            return false;
        }
    }
}
