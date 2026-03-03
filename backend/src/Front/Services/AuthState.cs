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
    private IJSRuntime? _jsRuntime;
    private LoginInfo? _loginInfo;

    public UserDto? CurrentUser => _loginInfo?.User;
    public string? AccessToken => _loginInfo?.Token;
    public DateTime? TokenExpiry => _loginInfo?.TokenExpiration;

    public bool IsAuthenticated => AccessToken != null && TokenExpiry > DateTime.UtcNow;
    public bool IsTokenNullOrExpired => TokenExpiry == null || TokenExpiry <= DateTime.UtcNow;

    // Event for state changes
    public event Action? OnChange;

    private void OnStateChanged() => OnChange?.Invoke();

    public void SetJsRuntime(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public async Task LoginAsync(string accessToken, DateTime expiresAtUtc, UserDto user)
    {
        ThrowIfNoJsRuntime();
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
        ThrowIfNoJsRuntime();
        await LocalStorageUtils.ClearLoginInfo(_jsRuntime!);
        _loginInfo = null;
        OnStateChanged();
    }

    public async Task<bool> LoadFromLocalStorageAsync()
    {
        ThrowIfNoJsRuntime();
        var loginInfo = await LocalStorageUtils.LoadLoginInfo(_jsRuntime!);
        if (loginInfo != null)
        {
            _loginInfo = loginInfo;
            Log.Information("Auth state loaded from localStorage.");
            return true;
        }
        else
        {
            Log.Information("No auth state found in localStorage.");
            return false;
        }
    }

    private void ThrowIfNoJsRuntime()
    {
        if (_jsRuntime == null)
        {
            Log.Error("JSRuntime is not set. Cannot perform localStorage operations.");
            throw new InvalidOperationException("JSRuntime is not set. Please call SetJSRuntime before using localStorage features.");
        }
    }
}
