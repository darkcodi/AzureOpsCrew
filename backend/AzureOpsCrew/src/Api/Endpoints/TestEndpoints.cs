using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints
{
    public static class TestEndpoints
    {
        public static void MapTestEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/test")
                .WithTags("Test");

            group.MapGet("/ping", () =>
            {
                return Results.Ok(new { message = "pong" });
            })
                .Produces(200, typeof(object));

            group.MapGet("/cosmos-health", async (AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                try
                {
                    await context.Dummies.CountAsync(cancellationToken);
                    return Results.Ok(new { status = "healthy", database = "cosmos" });
                }
                catch
                {
                    return Results.StatusCode(503);
                }
            })
                .Produces(200, typeof(object))
                .Produces(503);
        }
    }
}
