using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.Auth;
using AzureOpsCrew.Api.Endpoints.Filters;
using AzureOpsCrew.Domain.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequestDto body,
            AzureOpsCrewContext context,
            IPasswordHasher<User> passwordHasher,
            JwtTokenService jwtTokenService,
            CancellationToken cancellationToken) =>
        {
            var normalizedEmail = NormalizeEmail(body.Email);

            var exists = await context.Users
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (exists)
                return Results.Conflict(new { error = "Email is already registered." });

            var displayName = string.IsNullOrWhiteSpace(body.DisplayName)
                ? body.Email.Trim()
                : body.DisplayName.Trim();

            var user = new User(
                email: body.Email.Trim(),
                normalizedEmail: normalizedEmail,
                passwordHash: string.Empty,
                displayName: displayName);

            var hash = passwordHasher.HashPassword(user, body.Password);
            user.UpdatePasswordHash(hash);

            context.Users.Add(user);
            await context.SaveChangesAsync(cancellationToken);

            var token = jwtTokenService.CreateToken(user);
            return Results.Ok(ToAuthResponse(user, token));
        })
        .AddEndpointFilter<ValidationFilter<RegisterRequestDto>>()
        .Produces<AuthResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict)
        .AllowAnonymous();

        group.MapPost("/login", async (
            LoginRequestDto body,
            AzureOpsCrewContext context,
            IPasswordHasher<User> passwordHasher,
            JwtTokenService jwtTokenService,
            CancellationToken cancellationToken) =>
        {
            var normalizedEmail = NormalizeEmail(body.Email);

            var user = await context.Users
                .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (user is null || !user.IsActive)
                return Results.Unauthorized();

            var passwordResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, body.Password);
            if (passwordResult == PasswordVerificationResult.Failed)
                return Results.Unauthorized();

            if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
            {
                var rehash = passwordHasher.HashPassword(user, body.Password);
                user.UpdatePasswordHash(rehash);
            }

            user.MarkLogin();
            await context.SaveChangesAsync(cancellationToken);

            var token = jwtTokenService.CreateToken(user);
            return Results.Ok(ToAuthResponse(user, token));
        })
        .AddEndpointFilter<ValidationFilter<LoginRequestDto>>()
        .Produces<AuthResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
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

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static AuthResponseDto ToAuthResponse(User user, AuthTokenResult token)
    {
        return new AuthResponseDto(
            token.AccessToken,
            token.ExpiresAtUtc,
            new AuthUserDto(user.Id, user.Email, user.DisplayName));
    }
}
