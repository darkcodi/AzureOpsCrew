using System.Net.Http.Json;
using System.Text.Json;
using Front.Models;
using Serilog;

namespace Front.Services;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public UserDto? User { get; set; }
    public string? Error { get; set; }
}

public class RegisterChallengeResponse
{
    public string Message { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public int ResendAvailableInSeconds { get; set; }
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
            Log.Information($"Login response: {content}");

            if (response.IsSuccessStatusCode && result != null)
            {
                return result;
            }

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

    public async Task<RegisterChallengeResponse?> RegisterAsync(string email, string password, string username)
    {
        try
        {
            var request = new { email, password, username };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register", request);

            var content = await response.Content.ReadAsStringAsync();
            Log.Information("Register response: {Content}", content);

            var result = JsonSerializer.Deserialize<RegisterChallengeResponse>(content, _jsonOptions);

            if (response.IsSuccessStatusCode && result != null)
            {
                return result;
            }

            return result ?? new RegisterChallengeResponse { Error = ExtractErrorMessage(content, "Unable to start registration") };
        }
        catch (HttpRequestException ex)
        {
            Log.Error("HTTP error during registration: {Message}", ex.Message);
            return new RegisterChallengeResponse { Error = "Unable to connect to server" };
        }
        catch (Exception ex)
        {
            Log.Error("Error during registration: {Message}", ex.Message);
            return new RegisterChallengeResponse { Error = "Unable to start registration. Please try again." };
        }
    }

    public async Task<LoginResponse?> VerifyRegistrationAsync(string email, string code)
    {
        try
        {
            var request = new { email, code };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register/verify", request);

            var content = await response.Content.ReadAsStringAsync();
            Log.Information("Verify registration response: {Content}", content);

            var result = JsonSerializer.Deserialize<LoginResponse>(content, _jsonOptions);

            if (response.IsSuccessStatusCode && result != null)
            {
                return result;
            }

            return result ?? new LoginResponse { Error = ExtractErrorMessage(content, "Verification failed") };
        }
        catch (HttpRequestException ex)
        {
            Log.Error("HTTP error during verification: {Message}", ex.Message);
            return new LoginResponse { Error = "Unable to connect to server" };
        }
        catch (Exception ex)
        {
            Log.Error("Error during verification: {Message}", ex.Message);
            return new LoginResponse { Error = "Unable to verify code. Please try again." };
        }
    }

    public async Task<RegisterChallengeResponse?> ResendRegistrationCodeAsync(string email)
    {
        try
        {
            var request = new { email };
            var response = await _httpClient.PostAsJsonAsync("/api/auth/register/resend", request);

            var content = await response.Content.ReadAsStringAsync();
            Log.Information("Resend registration code response: {Content}", content);

            var result = JsonSerializer.Deserialize<RegisterChallengeResponse>(content, _jsonOptions);

            if (response.IsSuccessStatusCode && result != null)
            {
                return result;
            }

            return result ?? new RegisterChallengeResponse { Error = ExtractErrorMessage(content, "Unable to resend verification code") };
        }
        catch (HttpRequestException ex)
        {
            Log.Error("HTTP error during resend: {Message}", ex.Message);
            return new RegisterChallengeResponse { Error = "Unable to connect to server" };
        }
        catch (Exception ex)
        {
            Log.Error("Error during resend: {Message}", ex.Message);
            return new RegisterChallengeResponse { Error = "Unable to resend verification code. Please try again." };
        }
    }

    private string ExtractErrorMessage(string jsonContent, string fallback)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            if (root.TryGetProperty("error", out var errorProp) && errorProp.ValueKind == JsonValueKind.String)
                return errorProp.GetString() ?? fallback;
            if (root.TryGetProperty("Error", out var errorPropUpper) && errorPropUpper.ValueKind == JsonValueKind.String)
                return errorPropUpper.GetString() ?? fallback;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in errors.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        return prop.Value.GetString() ?? fallback;
                }
            }

            if (root.TryGetProperty("Errors", out var errorsUpper) && errorsUpper.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in errorsUpper.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.String)
                        return prop.Value.GetString() ?? fallback;
                }
            }
        }
        catch
        {
            // JSON parsing failed, use fallback
        }

        return fallback;
    }
}
