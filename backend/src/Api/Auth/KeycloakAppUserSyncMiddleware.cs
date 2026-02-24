using System.Security.Claims;

namespace AzureOpsCrew.Api.Auth;

public sealed class KeycloakAppUserSyncMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<KeycloakAppUserSyncMiddleware> _logger;

    public KeycloakAppUserSyncMiddleware(RequestDelegate next, ILogger<KeycloakAppUserSyncMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext, KeycloakAppUserSyncService syncService)
    {
        var user = httpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await _next(httpContext);
            return;
        }

        if (user.HasClaim(c => c.Type == AuthenticatedUserExtensions.AppUserIdClaimType))
        {
            await _next(httpContext);
            return;
        }

        var result = await syncService.EnsureUserAsync(user, httpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            httpContext.Response.StatusCode = result.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(
                new { error = result.Error ?? "Unauthorized" },
                cancellationToken: httpContext.RequestAborted);
            return;
        }

        if (user.Identity is ClaimsIdentity identity)
        {
            identity.AddClaim(new Claim(AuthenticatedUserExtensions.AppUserIdClaimType, result.UserId.ToString()));
            if (!string.IsNullOrWhiteSpace(result.DisplayName) && !user.HasClaim(c => c.Type == AuthenticatedUserExtensions.AppUserDisplayNameClaimType))
            {
                identity.AddClaim(new Claim(AuthenticatedUserExtensions.AppUserDisplayNameClaimType, result.DisplayName));
            }
        }
        else
        {
            _logger.LogWarning("Authenticated principal is not a ClaimsIdentity. Local AppUser claim was not attached.");
        }

        await _next(httpContext);
    }
}
