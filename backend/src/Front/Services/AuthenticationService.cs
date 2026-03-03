using System.Net.Http.Json;
using System.Text.Json;
using Front.Models;
using Serilog;

namespace Front.Services;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public UserDto User { get; set; } = new();
    public string? Error { get; set; }
}

public class AuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AuthenticationService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LoginResponse?> LoginAsync(string email, string password)
    {
        try
        {
            var loginRequest = new { email, password };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/login", loginRequest);

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);

            if (response.IsSuccessStatusCode && result != null)
            {
                return result;
            }

            // Return error response
            return result ?? new LoginResponse { Error = "Login failed" };
        }
        catch (HttpRequestException ex)
        {
            Log.Error($"HTTP error during login: {ex.Message}");
            return new LoginResponse { Error = "Unable to connect to server" };
        }
        catch (Exception ex)
        {
            Log.Error($"Error during login: {ex.Message}");
            return new LoginResponse { Error = "An unexpected error occurred" };
        }
    }
}
