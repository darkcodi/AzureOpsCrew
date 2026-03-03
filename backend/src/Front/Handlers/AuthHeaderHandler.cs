using System.Net.Http.Headers;
using Front.Services;

namespace Front.Handlers;

/// <summary>
/// Delegating handler that adds the Authorization header to all outgoing HTTP requests.
/// </summary>
public class AuthHeaderHandler : DelegatingHandler
{
    private readonly AuthState _authState;

    public AuthHeaderHandler(AuthState authState)
    {
        _authState = authState;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add Authorization header if a token exists
        if (_authState.AccessToken != null)
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _authState.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
