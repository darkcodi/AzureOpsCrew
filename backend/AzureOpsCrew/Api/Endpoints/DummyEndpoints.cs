using AzureOpsCrew.Api.Endpoints.Dtos.Dummies;

namespace AzureOpsCrew.Api.Endpoints
{
    public static class DummyEndpoints
    {
        public static void MapDummyEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/dummy")
                .WithTags("Dummy");

            group.MapPost("/create", async (CreateBodyDto body, CancellationToken cancellationToken) =>
            {
                var created = new DummyDto
                {
                    Id = Guid.NewGuid(),
                    Description = body.Description,
                    Name = body.Name,
                };

                return Results.Created($"/api/dummy/{created.Id}", created);
            })
                .Produces(201, typeof(DummyDto))
                .Produces(400);

            group.MapGet("/{Id}", async (Guid Id, CancellationToken cancellationToken) =>
            {
                var found = new DummyDto
                {
                    Id = Id,
                    Description = "Description dummy",
                    Name = "Name dummy",
                };

                return Results.Ok(found);
            })
                .Produces(200, typeof(DummyDto))
                .Produces(404);
        }
    }
}
