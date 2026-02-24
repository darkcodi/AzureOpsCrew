using System.IdentityModel.Tokens.Jwt;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Api.Setup.Seeds;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AzureOpsCrew.Api.Auth;

public sealed class KeycloakAppUserSyncService
{
    private const string Provider = "keycloak";
    private static readonly TimeSpan PresenceWriteThrottle = TimeSpan.FromMinutes(1);

    private readonly AzureOpsCrewContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly KeycloakOidcSettings _keycloakSettings;
    private readonly SeederOptions _seederOptions;

    public KeycloakAppUserSyncService(
        AzureOpsCrewContext context,
        IPasswordHasher<User> passwordHasher,
        IOptions<KeycloakOidcSettings> keycloakOptions,
        IOptions<SeederOptions> seederOptions)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _keycloakSettings = keycloakOptions.Value;
        _seederOptions = seederOptions.Value;
    }

    public async Task<KeycloakAppUserSyncResult> EnsureUserAsync(
        System.Security.Claims.ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var providerSubject = GetFirstClaimValue(
            principal,
            JwtRegisteredClaimNames.Sub,
            System.Security.Claims.ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(providerSubject))
            return KeycloakAppUserSyncResult.Fail(401, "Missing subject claim in access token.");

        var email = GetFirstClaimValue(
            principal,
            JwtRegisteredClaimNames.Email,
            System.Security.Claims.ClaimTypes.Email)?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            return KeycloakAppUserSyncResult.Fail(403, "Missing email claim in access token.");

        if (_keycloakSettings.RequireVerifiedEmail)
        {
            if (!TryGetBooleanClaim(principal, "email_verified", out var emailVerified))
                return KeycloakAppUserSyncResult.Fail(403, "Missing email verification claim in access token.");

            if (!emailVerified)
                return KeycloakAppUserSyncResult.Fail(403, "Email address is not verified.");
        }

        var normalizedEmail = NormalizeEmail(email);
        var displayName = ResolveDisplayName(principal, email);
        var now = DateTime.UtcNow;
        var createdUser = false;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var linkedIdentity = await _context.UserExternalIdentities
                    .SingleOrDefaultAsync(
                        x => x.Provider == Provider && x.ProviderSubject == providerSubject,
                        cancellationToken);

                User? user = null;
                if (linkedIdentity is not null)
                {
                    user = await _context.Users
                        .SingleOrDefaultAsync(u => u.Id == linkedIdentity.UserId, cancellationToken);

                    if (user is null)
                    {
                        _context.UserExternalIdentities.Remove(linkedIdentity);
                        await _context.SaveChangesAsync(cancellationToken);
                        linkedIdentity = null;
                    }
                }

                if (user is null)
                {
                    user = await _context.Users
                        .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

                    if (user is null)
                    {
                        user = new User(
                            email: email,
                            normalizedEmail: normalizedEmail,
                            passwordHash: string.Empty,
                            displayName: displayName);

                        var externalOnlyPasswordHash = _passwordHasher.HashPassword(user, Guid.NewGuid().ToString("N"));
                        user.UpdatePasswordHash(externalOnlyPasswordHash);

                        _context.Users.Add(user);
                        await _context.SaveChangesAsync(cancellationToken);
                        createdUser = true;
                    }
                }

                if (linkedIdentity is null)
                {
                    linkedIdentity = new UserExternalIdentity(user.Id, Provider, providerSubject, email);
                    _context.UserExternalIdentities.Add(linkedIdentity);
                }
                else
                {
                    linkedIdentity.UpdateEmail(email);
                }

                if (!string.IsNullOrWhiteSpace(displayName) &&
                    !string.Equals(user.DisplayName, displayName, StringComparison.Ordinal))
                {
                    user.UpdateDisplayName(displayName);
                }

                if (!user.IsActive)
                    return KeycloakAppUserSyncResult.Fail(403, "User is deactivated.");

                if (!user.LastLoginAt.HasValue || now - user.LastLoginAt.Value >= PresenceWriteThrottle)
                    user.MarkLogin();

                await _context.SaveChangesAsync(cancellationToken);

                if (createdUser)
                {
                    await UserWorkspaceDefaults.EnsureAsync(
                        _context,
                        _seederOptions,
                        user.Id,
                        cancellationToken);
                }

                return KeycloakAppUserSyncResult.Success(user.Id, user.Email, user.DisplayName);
            }
            catch (DbUpdateException) when (attempt == 0)
            {
                _context.ChangeTracker.Clear();
                createdUser = false;
            }
        }

        return KeycloakAppUserSyncResult.Fail(503, "Unable to synchronize user profile.");
    }

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string ResolveDisplayName(System.Security.Claims.ClaimsPrincipal principal, string email)
    {
        var givenName = GetFirstClaimValue(principal, "given_name")?.Trim();
        var familyName = GetFirstClaimValue(principal, "family_name")?.Trim();
        var combinedName = string.Join(
            " ",
            new[] { givenName, familyName }.Where(value => !string.IsNullOrWhiteSpace(value)));

        if (!string.IsNullOrWhiteSpace(combinedName))
            return combinedName;

        var fromClaims = GetFirstClaimValue(
            principal,
            "name",
            System.Security.Claims.ClaimTypes.Name,
            "preferred_username");

        if (!string.IsNullOrWhiteSpace(fromClaims))
        {
            var candidate = fromClaims.Trim();
            if (!string.Equals(candidate, "User", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(candidate, "Human", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        var atIndex = email.IndexOf('@');
        return atIndex > 0 ? email[..atIndex] : email;
    }

    private static string? GetFirstClaimValue(System.Security.Claims.ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static bool TryGetBooleanClaim(System.Security.Claims.ClaimsPrincipal principal, string claimType, out bool value)
    {
        var raw = principal.FindFirst(claimType)?.Value;
        if (string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase) || raw == "1")
        {
            value = true;
            return true;
        }

        if (string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) || raw == "0")
        {
            value = false;
            return true;
        }

        value = false;
        return false;
    }
}

public sealed record KeycloakAppUserSyncResult(
    bool IsSuccess,
    int StatusCode,
    int UserId,
    string? Email,
    string? DisplayName,
    string? Error)
{
    public static KeycloakAppUserSyncResult Success(int userId, string email, string displayName) =>
        new(true, StatusCodes.Status200OK, userId, email, displayName, null);

    public static KeycloakAppUserSyncResult Fail(int statusCode, string error) =>
        new(false, statusCode, 0, null, null, error);
}
