using AzureOpsCrew.Api.Endpoints.Dtos.Agents;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
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

            group.MapPost("/create", async (CreateAgentBodyDto body, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var providerAgentId = Guid.NewGuid().ToString("D");

                var agent = new Agent(
                    Guid.NewGuid(),
                    body.ClientId,
                    body.Info!,
                    body.ProviderId,
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

            group.MapPut("/{id}", async (Guid id, UpdateAgentBodyDto body, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

                if (found is null)
                {
                    return Results.NotFound();
                }

                if (body.Info is null)
                {
                    return Results.BadRequest("Info is required");
                }

                found.Update(body.Info, body.ProviderId, body.Color);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok(found);
            })
            .Produces<Agent>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

            group.MapDelete("/{id}", async (Guid id, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
            {
                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

                if (found is null)
                {
                    return Results.NotFound();
                }

                var agentIdString = found.Id.ToString();

                var allChannels = await context.Set<Channel>().ToListAsync(cancellationToken);
                var channelsWithAgent = allChannels
                    .Where(c => c.AgentIds.Contains(agentIdString))
                    .ToList();

                foreach (var channel in channelsWithAgent)
                {
                    channel.RemoveAgent(agentIdString);
                }

                context.Set<Agent>().Remove(found);
                await context.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
