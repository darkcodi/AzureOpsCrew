using AzureOpsCrew.Api.Auth;
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
                .WithTags("Agents")
                .RequireAuthorization();

            group.MapPost("/create", async (
                CreateAgentBodyDto body,
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetRequiredUserId();

                if (body.Info is null)
                    return Results.BadRequest("Info is required");

                var providerExists = await context.Providers
                    .AnyAsync(p => p.Id == body.ProviderId && p.ClientId == userId, cancellationToken);

                if (!providerExists)
                    return Results.BadRequest("Provider not found for current user.");

                var providerAgentId = Guid.NewGuid().ToString("D");

                var agent = new Agent(
                    Guid.NewGuid(),
                    userId,
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

            group.MapGet("", async (
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetRequiredUserId();

                var agents = await context.Set<Agent>()
                    .Where(a => a.ClientId == userId)
                    .OrderBy(a => a.DateCreated)
                    .ToListAsync(cancellationToken);

                return Results.Ok(agents);
            })
            .Produces<Agent[]>(StatusCodes.Status200OK);

            group.MapGet("/{id}", async (
                Guid id,
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetRequiredUserId();

                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id && a.ClientId == userId, cancellationToken);

                return found is null ? Results.NotFound() : Results.Ok(found);
            })
            .Produces<Agent>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapPut("/{id}", async (
                Guid id,
                UpdateAgentBodyDto body,
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetRequiredUserId();

                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id && a.ClientId == userId, cancellationToken);

                if (found is null)
                    return Results.NotFound();

                if (body.Info is null)
                    return Results.BadRequest("Info is required");

                var providerExists = await context.Providers
                    .AnyAsync(p => p.Id == body.ProviderId && p.ClientId == userId, cancellationToken);

                if (!providerExists)
                    return Results.BadRequest("Provider not found for current user.");

                found.Update(body.Info, body.ProviderId, body.Color);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok(found);
            })
            .Produces<Agent>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

            group.MapDelete("/{id}", async (
                Guid id,
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetRequiredUserId();

                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id && a.ClientId == userId, cancellationToken);

                if (found is null)
                    return Results.NotFound();

                var agentIdString = found.Id.ToString();

                var userChannels = await context.Set<Channel>()
                    .Where(c => c.ClientId == userId)
                    .ToListAsync(cancellationToken);

                var channelsWithAgent = userChannels
                    .Where(c => c.AgentIds.Contains(agentIdString))
                    .ToList();

                foreach (var channel in channelsWithAgent)
                    channel.RemoveAgent(agentIdString);

                context.Set<Agent>().Remove(found);
                await context.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
