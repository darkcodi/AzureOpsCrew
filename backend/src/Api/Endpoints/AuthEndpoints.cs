using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.Auth;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class AuthEndpoints
{
    private const string DemoEmail = "demo@azureopscrew.dev";
    private const string DemoNormalizedEmail = "DEMO@AZUREOPSCREW.DEV";
    private const string DemoDisplayName = "Demo User";
    private const string DemoPassword = "AzureOpsCrew2025!";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/auth")
            .WithTags("Auth");

        // Auto-login: find or create demo user, return JWT immediately
        group.MapPost("/auto-login", async (
            AzureOpsCrewContext context,
            IPasswordHasher<User> passwordHasher,
            JwtTokenService jwtTokenService,
            CancellationToken cancellationToken) =>
        {
            var user = await context.Users
                .SingleOrDefaultAsync(u => u.NormalizedEmail == DemoNormalizedEmail, cancellationToken);

            if (user is null)
            {
                user = new User(
                    email: DemoEmail,
                    normalizedEmail: DemoNormalizedEmail,
                    passwordHash: string.Empty,
                    displayName: DemoDisplayName);

                var hash = passwordHasher.HashPassword(user, DemoPassword);
                user.UpdatePasswordHash(hash);
                context.Users.Add(user);
                await context.SaveChangesAsync(cancellationToken);
            }

            user.MarkLogin();
            await context.SaveChangesAsync(cancellationToken);

            var token = jwtTokenService.CreateToken(user);
            return Results.Ok(new AuthResponseDto(
                token.AccessToken,
                token.ExpiresAtUtc,
                new AuthUserDto(user.Id, user.Email, user.DisplayName)));
        })
        .Produces<AuthResponseDto>(StatusCodes.Status200OK)
        .AllowAnonymous();

        // Keep legacy login for backwards compatibility
        group.MapPost("/login", async (
            LoginRequestDto body,
            AzureOpsCrewContext context,
            IPasswordHasher<User> passwordHasher,
            JwtTokenService jwtTokenService,
            CancellationToken cancellationToken) =>
        {
            var normalizedEmail = body.Email.Trim().ToUpperInvariant();

            var user = await context.Users
                .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            var passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, body.Password);
            if (passwordResult == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            user.MarkLogin();
            await context.SaveChangesAsync(cancellationToken);

            var token = jwtTokenService.CreateToken(user);
            return Results.Ok(new AuthResponseDto(
                token.AccessToken,
                token.ExpiresAtUtc,
                new AuthUserDto(user.Id, user.Email, user.DisplayName)));
        })
        .Produces<AuthResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .AllowAnonymous();

        group.MapGet("/me", async (
            HttpContext httpContext,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();

            var user = await context.Users
                .SingleOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);

            if (user is null)
                return Results.Unauthorized();

            var now = DateTime.UtcNow;
            if (!user.LastLoginAt.HasValue || now - user.LastLoginAt.Value >= TimeSpan.FromMinutes(1))
            {
                user.MarkLogin();
                await context.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(new AuthUserDto(user.Id, user.Email, user.DisplayName));
        })
        .Produces<AuthUserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
    }
}
