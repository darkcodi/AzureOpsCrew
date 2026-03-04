using System.Net.Http.Json;
using Front.Models;
using Serilog;

namespace Front.Services;

public class AgentService
{
    private readonly HttpClient _httpClient;

    public AgentService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<List<AgentDto>> GetAgentsAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<AgentDto>>("/api/agents");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching agents: {ex.Message}");
            return [];
        }
    }

    public async Task<AgentDto?> GetAgentAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AgentDto>($"/api/agents/{id}");
        }
        catch (Exception ex)
        {
            Log.Error($"Error fetching agent {id}: {ex.Message}");
            return null;
        }
    }

    public async Task<AgentDto?> CreateAgentAsync(string username, string prompt, string model, Guid providerId, string color, string? description)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/agents/create",
                new
                {
                    Username = username,
                    Prompt = prompt,
                    Model = model,
                    ProviderId = providerId,
                    Color = color,
                    Description = description
                });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AgentDto>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error creating agent: {ex.Message}");
            return null;
        }
    }

    public async Task<AgentDto?> UpdateAgentAsync(Guid id, string username, string prompt, string model, Guid providerId, string color, string? description)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/agents/{id}",
                new
                {
                    Username = username,
                    Prompt = prompt,
                    Model = model,
                    ProviderId = providerId,
                    Color = color,
                    Description = description
                });

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AgentDto>();
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error updating agent: {ex.Message}");
            return null;
        }
    }

    public async Task<bool> DeleteAgentAsync(Guid id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/agents/{id}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Error($"Error deleting agent {id}: {ex.Message}");
            return false;
        }
    }
}
