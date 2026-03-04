using Front.Models;
using Microsoft.JSInterop;
using Serilog;

namespace Front.Utils;

public static class LocalStorageUtils
{
    public static async Task PersistLoginInfo(IJSRuntime jsRuntime, LoginInfo loginInfo)
    {
        try
        {
            Log.Information("Persisting auth state to localStorage for user {Email}.", loginInfo.User.Email);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token", loginInfo.Token);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_token_expiry", loginInfo.TokenExpiration.ToString("o"));
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user_id", loginInfo.User.Id.ToString());
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user_email", loginInfo.User.Email);
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", "auth_user_username", loginInfo.User.Username);
            Log.Information("Auth state persisted to localStorage successfully.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error persisting auth state to localStorage: {ex.Message}");
            throw;
        }
    }

    public static async Task<LoginInfo?> LoadLoginInfo(IJSRuntime jsRuntime)
    {
        try
        {
            Log.Information("Loading auth state from localStorage.");
            var token = await jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_token");
            var expiryStr = await jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_token_expiry");
            var userIdStr = await jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_user_id");
            var email = await jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_user_email");
            var username = await jsRuntime.InvokeAsync<string>("localStorage.getItem", "auth_user_username");

            if (!string.IsNullOrEmpty(token) &&
                DateTime.TryParse(expiryStr, out var expiry) &&
                Guid.TryParse(userIdStr, out var userId))
            {
                Log.Information("Auth state found in localStorage, validating token expiry.");
                if (expiry > DateTime.UtcNow)
                {
                    Log.Information("Auth token in localStorage is valid, loading user info.");
                    return new LoginInfo
                    {
                        Token = token,
                        TokenExpiration = expiry,
                        User = new UserDto
                        {
                            Id = userId,
                            Email = email ?? string.Empty,
                            Username = username ?? string.Empty
                        }
                    };
                }
                else
                {
                    Log.Information("Auth token in localStorage has expired, clearing it.");
                    await ClearLoginInfo(jsRuntime);
                }
            }
            else
            {
                Log.Information("No valid auth state found in localStorage.");
            }

            return null;
        }
        catch (Exception ex)
        {
            Log.Error($"Error loading auth state from localStorage: {ex.Message}");
            throw;
        }
    }

    public static async Task ClearLoginInfo(IJSRuntime jsRuntime)
    {
        try
        {
            Log.Information("Clearing auth state from localStorage.");
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token");
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_token_expiry");
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_user_id");
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_user_email");
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", "auth_user_username");
            Log.Information("Auth state cleared from localStorage.");
        }
        catch (Exception ex)
        {
            Log.Error($"Error clearing auth state from localStorage: {ex.Message}");
            throw;
        }
    }
}
