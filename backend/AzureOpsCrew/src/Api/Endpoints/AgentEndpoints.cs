using AzureOpsCrew.Api.Endpoints.Dtos.Agents;
using AzureOpsCrew.Domain.AgentManagements;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints
{
    public static class AgentEndpoints
    {
        public static void MapAgentEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/agents")
                .WithTags("Agents");

            group.MapPost("/create", async (CreateAgentBodyDto body, IAgentFactory agentFactory,AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var providerAgentId = await agentFactory.Create(body.Provider, body.Info!, cancellationToken);

                var agent = new Agent(
                    Guid.NewGuid(),
                    body.ClientId,
                    body.Info!,
                    body.Provider,
                    providerAgentId,
                    body.Color
                );

                await context.AddAsync(agent, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Created($"/api/agents/{agent.Id}", agent);
            })
            .Produces<Agent>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

            group.MapGet("", async (int clientId, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var agents = await context.Set<Agent>()
                    .Where(a => a.ClientId == clientId)
                    .OrderBy(a => a.DateCreated)
                    .ToListAsync(cancellationToken);

                return Results.Ok(agents);
            })
            .Produces<Agent[]>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

            group.MapGet("/{id}", async (Guid id, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

                return found is null ? Results.NotFound() : Results.Ok(found);
            })
            .Produces<Agent>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
