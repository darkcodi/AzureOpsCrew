using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.Auth;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/auth")
            .WithTags("Auth");

        group.MapPost("/register", () => LegacyEmailPasswordAuthDisabled())
            .Produces(StatusCodes.Status410Gone)
            .AllowAnonymous();

        group.MapPost("/register/resend", () => LegacyEmailPasswordAuthDisabled())
            .Produces(StatusCodes.Status410Gone)
            .AllowAnonymous();

        group.MapPost("/register/verify", () => LegacyEmailPasswordAuthDisabled())
            .Produces(StatusCodes.Status410Gone)
            .AllowAnonymous();

        group.MapPost("/login", () => LegacyEmailPasswordAuthDisabled())
            .Produces(StatusCodes.Status410Gone)
            .AllowAnonymous();

        group.MapPost("/keycloak/exchange", () =>
            Results.Json(
                new { error = "Deprecated. Frontend must use Keycloak-issued access tokens directly." },
                statusCode: StatusCodes.Status410Gone))
            .Produces(StatusCodes.Status410Gone)
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

            return Results.Ok(new AuthUserDto(user.Id, user.Email, user.DisplayName));
        })
        .Produces<AuthUserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .RequireAuthorization();
    }

    private static IResult LegacyEmailPasswordAuthDisabled() =>
        Results.Json(
            new { error = "Email/password authentication is disabled. Use Keycloak sign-in." },
            statusCode: StatusCodes.Status410Gone);
}
