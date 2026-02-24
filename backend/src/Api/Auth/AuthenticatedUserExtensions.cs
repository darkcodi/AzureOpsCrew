using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace AzureOpsCrew.Api.Auth;

public static class AuthenticatedUserExtensions
{
    public const string AppUserIdClaimType = "aoc_user_id";
    public const string AppUserDisplayNameClaimType = "aoc_display_name";

    public static int GetRequiredUserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirst(AppUserIdClaimType)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(id) || !int.TryParse(id, out var userId))
            throw new InvalidOperationException("Missing or invalid authenticated user id.");

        return userId;
    }
}
