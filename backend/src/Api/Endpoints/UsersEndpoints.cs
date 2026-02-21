using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.Users;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class UsersEndpoints
{
    private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PresenceWriteThrottle = TimeSpan.FromMinutes(1);

    public static void MapUsersEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("", async (
            HttpContext httpContext,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var now = DateTime.UtcNow;
            var currentUserId = httpContext.User.GetRequiredUserId();

            // Keep presence reasonably fresh without writing on every request.
            var currentUser = await context.Users
                .SingleOrDefaultAsync(u => u.Id == currentUserId && u.IsActive, cancellationToken);

            if (currentUser is null)
                return Results.Unauthorized();

            if (!currentUser.LastLoginAt.HasValue || now - currentUser.LastLoginAt.Value >= PresenceWriteThrottle)
            {
                currentUser.MarkLogin();
                await context.SaveChangesAsync(cancellationToken);
            }

            var users = await context.Users
                .AsNoTracking()
                .Where(u => u.IsActive)
                .OrderBy(u => u.DisplayName)
                .Select(u => new UserPresenceDto(
                    u.Id,
                    u.DisplayName,
                    u.Email,
                    u.LastLoginAt.HasValue && now - u.LastLoginAt.Value <= OnlineWindow,
                    u.Id == currentUserId,
                    u.LastLoginAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(users);
        })
        .Produces<IReadOnlyList<UserPresenceDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
