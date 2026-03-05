using Microsoft.JSInterop;
using Front.Models;
using Front.Utils;
using Serilog;

namespace Front.Services;

/// <summary>
/// Centralized state management for authentication.
/// </summary>
public class AuthState
{
    private readonly IJSRuntime _jsRuntime;
    private LoginInfo? _loginInfo;
    private readonly TaskCompletionSource<bool> _initializationTcs = new();

    public AuthState(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Ensures the auth state has been initialized from localStorage before accessing the token.
    /// This prevents race conditions where HTTP requests are made before the token is loaded.
    /// </summary>
    public Task EnsureInitializedAsync() => _initializationTcs.Task;

    public UserDto? CurrentUser => _loginInfo?.User;
    public string? AccessToken => _loginInfo?.Token;
    public DateTime? TokenExpiry => _loginInfo?.TokenExpiration;

    public bool IsAuthenticated => AccessToken != null && TokenExpiry > DateTime.UtcNow;
    public bool IsTokenNullOrExpired => TokenExpiry == null || TokenExpiry <= DateTime.UtcNow;

    // Event for state changes
    public event Action? OnChange;

    private void OnStateChanged() => OnChange?.Invoke();

    public async Task LoginAsync(string accessToken, DateTime expiresAtUtc, UserDto user)
    {
        var loginInfo = new LoginInfo
        {
            Token = accessToken,
            TokenExpiration = expiresAtUtc,
            User = user,
        };
        await LocalStorageUtils.PersistLoginInfo(_jsRuntime!, loginInfo);
        _loginInfo = loginInfo;
        OnStateChanged();
    }

    public async Task LogoutAsync()
    {
        await LocalStorageUtils.ClearLoginInfo(_jsRuntime!);
        _loginInfo = null;
        OnStateChanged();
    }

    public async Task<bool> LoadFromLocalStorageAsync()
    {
        var loginInfo = await LocalStorageUtils.LoadLoginInfo(_jsRuntime!);
        if (loginInfo != null)
        {
            _loginInfo = loginInfo;
            Log.Information("Auth state loaded from localStorage.");
            _initializationTcs.TrySetResult(true);
            return true;
        }
        else
        {
            Log.Information("No auth state found in localStorage.");
            _initializationTcs.TrySetResult(false);
            return false;
        }
    }
}
