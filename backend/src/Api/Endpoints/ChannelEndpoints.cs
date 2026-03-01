using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Chat;
using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Ai.Models;
using AzureOpsCrew.Infrastructure.Ai.Workflows;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Temporalio.Client;

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
            IChatServerClient chatServerClient,
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

            // Create a chat with all agent IDs as participants
            var participantIds = body.AgentIds;
            var chat = await chatServerClient.CreateChatAsync(body.Name, participantIds, cancellationToken);

            // Use the returned chat's ID as the channel's ID (like DMs)
            var channel = new Channel(chat.Id, body.Name)
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
            IChatServerClient chatServerClient,
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

            // Add agent to the chat
            await chatServerClient.AddParticipantAsync(id, body.AgentId, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok();
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/{id}/remove-agent", async (
            Guid id,
            RemoveAgentBodyDto body,
            AzureOpsCrewContext context,
            IChatServerClient chatServerClient,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.BadRequest($"Unknown channel with id: {id}");

            channel.RemoveAgent(body.AgentId.ToString("D"));

            // Remove agent from the chat
            await chatServerClient.RemoveParticipantAsync(id, body.AgentId, cancellationToken);

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
            IChatServerClient chatServerClient,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.Ok(new List<ChatMessageEntity>());

            var messages = await chatServerClient.GetMessagesAsync(channel.Id, cancellationToken);
            return Results.Ok(messages);
        })
        .Produces<List<ChatMessageEntity>>(StatusCodes.Status200OK);

        group.MapPost("/{id}/messages", async (
            Guid id,
            CreateDirectMessageDto dto,
            HttpContext httpContext,
            AzureOpsCrewContext context,
            IChatServerClient chatServerClient,
            IOptions<TemporalSettings> temporalSettings,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (channel is null)
                return Results.NotFound();

            var senderId = httpContext.User.GetRequiredUserId();
            var message = await chatServerClient.CreateMessageAsync(channel.Id, dto.Content, senderId, cancellationToken);

            // Trigger all agents in the channel
            var client = await TemporalClient.ConnectAsync(new(temporalSettings.Value.GetTarget()));
            foreach (var agentIdString in channel.AgentIds)
            {
                if (Guid.TryParse(agentIdString, out var agentId))
                {
                    await AgentCoordinatorWorkflow.EnsureCoordinatorStartedAsync(client, agentId);
                    var trigger = new TriggerEvent(
                        TriggerId: Guid.NewGuid(),
                        Source: TriggerSource.DirectMessage,
                        CreatedAt: DateTime.UtcNow,
                        ThreadId: agentId,
                        RunId: Guid.NewGuid(),
                        Text: $"New message in channel '{channel.Name}'. Please check the message and respond accordingly. Use tool read_chat_messages to read the message content. ChatId: {channel.Id}, MessageId: {message.Id}"
                    );
                    var handle = client.GetWorkflowHandle<AgentCoordinatorWorkflow>(AgentCoordinatorWorkflow.WorkflowId(agentId));
                    await handle.SignalAsync(wf => wf.EnqueueAsync(trigger));
                }
            }

            return Results.Created($"/api/channels/{id}/messages/{message.Id}", message);
        })
        .Produces<ChatMessageEntity>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound);
    }
}
