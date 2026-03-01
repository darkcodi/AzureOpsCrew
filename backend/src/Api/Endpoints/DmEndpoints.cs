using AzureOpsCrew.Api.Chat;
using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class DmEndpoints
{
    public static void MapDmEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/users/{userId}/dms")
            .WithTags("DirectMessages")
            .RequireAuthorization();

        // GET: /api/users/{userId}/dms - Returns all DM channels where the specified user/agent is a participant
        group.MapGet("", async (
            Guid userId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var dms = await context.Dms
                .Where(dm => dm.User1Id == userId
                          || dm.User2Id == userId)
                .OrderBy(dm => dm.CreatedAt)
                .ToListAsync(cancellationToken);

            return Results.Ok(dms);
        })
        .Produces<DirectMessageChannel[]>(StatusCodes.Status200OK);

        // GET: /api/users/{userId}/dms/users/{otherUserId}/messages - Returns messages between two users
        group.MapGet("/users/{otherUserId}/messages", async (
            Guid userId,
            Guid otherUserId,
            AzureOpsCrewContext context,
            IChatServerClient chatServerClient,
            CancellationToken cancellationToken) =>
        {
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.User2Id == otherUserId) ||
                    (dm.User1Id == otherUserId && dm.User2Id == userId),
                    cancellationToken);

            if (dm is null)
                return Results.Ok(new List<ChatMessageEntity>());

            var messages = await chatServerClient.GetMessagesAsync(dm.Id, cancellationToken);
            return Results.Ok(messages);
        })
        .Produces<List<ChatMessageEntity>>(StatusCodes.Status200OK);

        // GET: /api/users/{userId}/dms/agents/{agentId}/messages - Returns messages between a user and an agent
        group.MapGet("/agents/{agentId}/messages", async (
            Guid userId,
            Guid agentId,
            AzureOpsCrewContext context,
            IChatServerClient chatServerClient,
            CancellationToken cancellationToken) =>
        {
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User1Id == userId && dm.Agent2Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent2Id == agentId),
                    cancellationToken);

            if (dm is null)
                return Results.Ok(new List<ChatMessageEntity>());

            var messages = await chatServerClient.GetMessagesAsync(dm.Id, cancellationToken);
            return Results.Ok(messages);
        })
        .Produces<List<ChatMessageEntity>>(StatusCodes.Status200OK);

        // POST: /api/users/{userId}/dms/users/{otherUserId}/messages - Posts a message between two users
        group.MapPost("/users/{otherUserId}/messages", async (
            Guid userId,
            Guid otherUserId,
            CreateDirectMessageDto dto,
            AzureOpsCrewContext context,
            IChatServerClient chatServerClient,
            CancellationToken cancellationToken) =>
        {
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.User2Id == otherUserId) ||
                    (dm.User1Id == otherUserId && dm.User2Id == userId),
                    cancellationToken);

            // Create DM channel if it doesn't exist
            if (dm is null)
            {
                var chat = await chatServerClient.CreateChatAsync($"DM_{userId}_{otherUserId}", cancellationToken);
                dm = new DirectMessageChannel
                {
                    Id = chat.Id,
                    User1Id = userId,
                    User2Id = otherUserId,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Dms.AddAsync(dm, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            var message = await chatServerClient.CreateMessageAsync(dm.Id, dto.Content, cancellationToken);
            return Results.Created($"/api/users/{userId}/dms/users/{otherUserId}/messages/{message.Id}", message);
        })
        .Produces<ChatMessageEntity>(StatusCodes.Status201Created);

        // POST: /api/users/{userId}/dms/agents/{agentId}/messages - Posts a message between a user and an agent
        group.MapPost("/agents/{agentId}/messages", async (
            Guid userId,
            Guid agentId,
            CreateDirectMessageDto dto,
            AzureOpsCrewContext context,
            IChatServerClient chatServerClient,
            CancellationToken cancellationToken) =>
        {
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User1Id == userId && dm.Agent2Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent2Id == agentId),
                    cancellationToken);

            // Create DM channel if it doesn't exist
            if (dm is null)
            {
                var chat = await chatServerClient.CreateChatAsync($"DM_{userId}_Agent_{agentId}", cancellationToken);
                dm = new DirectMessageChannel
                {
                    Id = chat.Id,
                    User1Id = userId,
                    Agent1Id = agentId,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Dms.AddAsync(dm, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            var message = await chatServerClient.CreateMessageAsync(dm.Id, dto.Content, cancellationToken);
            return Results.Created($"/api/users/{userId}/dms/agents/{agentId}/messages/{message.Id}", message);
        })
        .Produces<ChatMessageEntity>(StatusCodes.Status201Created);
    }
}
