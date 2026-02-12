using AzureOpsCrew.Api.Endpoints.Dtos.Dummies;
using AzureOpsCrew.Domain.Dimmies;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints
{
    public static class DummyEndpoints
    {
        public static void MapDummyEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/dummy")
                .WithTags("Dummy");

            group.MapPost("/create", async (CreateBodyDto body, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var dummy = new Dummy(Guid.NewGuid(), body.Name)
                {
                    Description = body.Description
                };

                await context.AddAsync(dummy);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Created($"/api/dummy/{dummy.Id}", dummy);
            })
                .Produces(201, typeof(Dummy))
                .Produces(400);

            group.MapGet("/{Id}", async (Guid Id, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var found = await context.Dummies.SingleOrDefaultAsync(d => d.Id == Id, cancellationToken);

                return found is null
                    ? Results.NotFound()
                    : Results.Ok(found);
            })
                .Produces(200, typeof(Dummy))
                .Produces(404);
        }
    }
}
