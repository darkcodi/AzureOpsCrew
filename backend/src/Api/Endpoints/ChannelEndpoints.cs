using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/channels")
            .WithTags("Channels");

        group.MapPost("/create", async (
            CreateChannelBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            body.AgentIds = body.AgentIds.Distinct().ToArray();

            if (body.AgentIds.Length > 0)
            {
                var agentsCount = await context.Set<Agent>()
                    .CountAsync(a => body.AgentIds.Contains(a.Id) && a.ClientId == body.ClientId, cancellationToken);

                if (agentsCount != body.AgentIds.Length)
                    return Results.BadRequest("One or more AgentIds are invalid or do not belong to the client.");
            }

            var channel = new Channel(Guid.NewGuid(), body.ClientId, body.Name)
            {
                Description = body.Description,
                AgentIds = body.AgentIds.Select(a => a.ToString("D")).ToArray()
            };

            await context.AddAsync(channel, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // draft output: guid
            return Results.Created($"/api/channels/{channel.Id}", new { channelId = channel.Id });
        })
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/{id}/add-agent", async (
            Guid id,
            AddAgentBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.BadRequest($"Unknown channel with id: {id}");

            channel.AddAgent(body.AgentId.ToString("D"));

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok();
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/{id}/remove-agent", async (
            Guid id,
            AddAgentBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.BadRequest($"Unknown channel with id: {id}");

            channel.RemoveAgent(body.AgentId.ToString("D"));

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok();
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("", async (
            int clientId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channels = await context.Set<Channel>()
                .Where(c => c.ClientId == clientId)
                .OrderBy(c => c.DateCreated)
                .ToListAsync(cancellationToken);

            return Results.Ok(channels);
        })
        .Produces<Channel[]>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            return channel is null ? Results.NotFound() : Results.Ok(channel);
        })
        .Produces<Channel>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
