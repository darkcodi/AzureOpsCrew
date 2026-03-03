using Microsoft.JSInterop;
using Front.Models;
using Serilog;

namespace Front.Services;

/// <summary>
/// Centralized state management for authentication.
/// </summary>
public class AuthState
{
    private UserDto? _currentUser;
    private string? _accessToken;
    private DateTime _tokenExpiry;
    private IJSRuntime? _jsRuntime;

    public bool IsAuthenticated => AccessToken != null && TokenExpiry > DateTime.UtcNow;

    public UserDto? CurrentUser
    {
        get => _currentUser;
        set
        {
            _currentUser = value;
            OnStateChanged();
        }
    }

    public string? AccessToken
    {
        get => _accessToken;
        set
        {
            _accessToken = value;
            OnStateChanged();
        }
    }

    public DateTime TokenExpiry
    {
        get => _tokenExpiry;
        set
        {
            _tokenExpiry = value;
            OnStateChanged();
        }
    }

    // Event for state changes
    public event Action? OnChange;

    private void OnStateChanged() => OnChange?.Invoke();

    public void SetJSRuntime(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public void Login(string accessToken, DateTime expiresAtUtc, UserDto user)
    {
        AccessToken = accessToken;
        TokenExpiry = expiresAtUtc;
        CurrentUser = user;

        // Schedule persistence to run async after context is available
        _ = Task.Run(async () =>
        {
            if (_jsRuntime != null)
            {
                await PersistToLocalStorageAsync();
            }
        });
    }

    public void Logout()
    {
        AccessToken = null;
        CurrentUser = null;
        TokenExpiry = DateTime.MinValue;

        // Schedule clearing to run async
        _ = Task.Run(async () =>
        {
            if (_jsRuntime != null)
            {
                await ClearFromLocalStorageAsync();
            }
        });
    }

    public bool IsTokenExpired()
    {
        return TokenExpiry <= DateTime.UtcNow;
    }

    private async Task PersistToLocalStorageAsync()
    {
        try
        {
            if (_jsRuntime != null && AccessToken != null && CurrentUser != null)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token", AccessToken);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_expiry", TokenExpiry.ToString("o"));
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user_id", CurrentUser.Id.ToString());
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user_email", CurrentUser.Email);
                await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user_username", CurrentUser.Username);
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error persisting auth state to localStorage: {ex.Message}");
        }
    }

    private async Task ClearFromLocalStorageAsync()
    {
        try
        {
            if (_jsRuntime != null)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token_expiry");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_user_id");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_user_email");
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_user_username");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error clearing auth state from localStorage: {ex.Message}");
        }
    }

    public async Task LoadFromLocalStorageAsync()
    {
        try
        {
            if (_jsRuntime == null) return;

            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_token");
            var expiryStr = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_token_expiry");
            var userIdStr = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_user_id");
            var email = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_user_email");
            var username = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_user_username");

            if (!string.IsNullOrEmpty(token) &&
                DateTime.TryParse(expiryStr, out var expiry) &&
                Guid.TryParse(userIdStr, out var userId))
            {
                if (expiry > DateTime.UtcNow)
                {
                    AccessToken = token;
                    TokenExpiry = expiry;
                    CurrentUser = new UserDto
                    {
                        Id = userId,
                        Email = email ?? string.Empty,
                        Username = username ?? string.Empty
                    };
                }
                else
                {
                    // Token expired, clear storage
                    await ClearFromLocalStorageAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error loading auth state from localStorage: {ex.Message}");
        }
    }
}
