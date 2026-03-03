using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Background;
using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Ai.Models;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AzureOpsCrew.Api.Endpoints;

public static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/channels")
            .WithTags("Channels")
            .RequireAuthorization();

        group.MapPost("/create", async (
            CreateChannelBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            body.AgentIds = body.AgentIds.Distinct().ToArray();

            if (body.AgentIds.Length > 0)
            {
                var agentsCount = await context.Set<Agent>()
                    .CountAsync(a => body.AgentIds.Contains(a.Id), cancellationToken);

                if (agentsCount != body.AgentIds.Length)
                    return Results.BadRequest("One or more AgentIds are invalid.");
            }

            var channel = new Channel(Guid.NewGuid(), body.Name)
            {
                Description = body.Description,
                AgentIds = body.AgentIds.Select(a => a.ToString("D")).ToArray()
            };

            await context.AddAsync(channel, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

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

            var agentExists = await context.Set<Agent>()
                .AnyAsync(a => a.Id == body.AgentId, cancellationToken);

            if (!agentExists)
                return Results.BadRequest($"Unknown agent with id: {body.AgentId}");

            var agentId = body.AgentId.ToString("D");
            if (!channel.AgentIds.Contains(agentId))
                channel.AddAgent(agentId);

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok();
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/{id}/remove-agent", async (
            Guid id,
            RemoveAgentBodyDto body,
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
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channels = await context.Set<Channel>()
                .OrderBy(c => c.DateCreated)
                .ToListAsync(cancellationToken);

            return Results.Ok(channels);
        })
        .Produces<Channel[]>(StatusCodes.Status200OK);

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

        group.MapDelete("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.NotFound();

            context.Set<Channel>().Remove(channel);
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/messages", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Channels
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.Ok(new List<Message>());

            var messages = await context.Messages
                .Where(m => m.ChannelId == channel.Id)
                .OrderBy(m => m.PostedAt)
                .ToListAsync(cancellationToken);
            return Results.Ok(messages);
        })
        .Produces<List<Message>>(StatusCodes.Status200OK);

        group.MapPost("/{id}/messages", async (
            Guid id,
            CreateDirectMessageDto dto,
            HttpContext httpContext,
            AzureOpsCrewContext context,
            AgentTriggerQueue agentTriggerQueue,
            IChannelEventBroadcaster channelEventBroadcaster,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.NotFound();

            var senderId = httpContext.User.GetRequiredUserId();
            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == senderId, cancellationToken);
            var message = new Message
            {
                Id = Guid.NewGuid(),
                Text = dto.Content,
                PostedAt = DateTime.UtcNow,
                UserId = senderId,
                ChannelId = channel.Id,
                AuthorName = user?.Username,
            };
            await context.Messages.AddAsync(message, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // Broadcast the new message via SignalR
            await channelEventBroadcaster.BroadcastMessageAddedAsync(channel.Id, message);

            // Trigger all agents in the channel
            foreach (var agentIdString in channel.AgentIds)
            {
                if (Guid.TryParse(agentIdString, out var agentId))
                {
                    agentTriggerQueue.Enqueue(agentId, channel.Id);
                }
            }

            return Results.Created($"/api/channels/{id}/messages/{message.Id}", message);
        })
        .Produces<Message>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound);
    }
}
