using System.Net.Http.Json;
using Front.Models;

namespace Front.Services;

public class UserService
{
    private readonly HttpClient _httpClient;

    public UserService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UserDto?> GetCurrentUserAsync()
    {
        // For now, return a mock user
        // TODO: Implement JWT token parsing for actual user
        return new UserDto
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Email = "user@example.com",
            Username = "CurrentUser",
            IsActive = true,
            IsOnline = true
        };
    }

    public async Task<List<UserDto>> GetUsersAsync()
    {
        try
        {
            var response = await _httpClient.GetFromJsonAsync<List<UserDto>>("/api/users");
            return response ?? [];
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching users: {ex.Message}");
            return [];
        }
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<UserDto>($"/api/users/{id}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching user {id}: {ex.Message}");
            return null;
        }
    }
}
