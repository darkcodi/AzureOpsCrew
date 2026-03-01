using System.Security.Claims;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Db;
using Chat.Auth;
using Chat.Endpoints.Dtos;
using Microsoft.EntityFrameworkCore;

namespace Chat.Endpoints
{
    public static class ChatEndpoints
    {
        public static void MapChatEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/chat")
                .WithTags("Chat")
                .RequireAuthorization();

            // GET: List all chats
            group.MapGet("/chats", async (
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chats = await context.Chats
                    .OrderBy(c => c.CreatedAt)
                    .ToListAsync(cancellationToken);

                return Results.Ok(chats);
            })
            .Produces<List<ChatEntity>>(StatusCodes.Status200OK);

            // GET: Get a specific chat by id
            group.MapGet("/chats/{id:guid}", async (
                Guid id,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                return chat is null ? Results.NotFound() : Results.Ok(chat);
            })
            .Produces<ChatEntity>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            // POST: Create a new chat
            group.MapPost("/chats", async (
                CreateChatDto dto,
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetRequiredUserId();
                var chat = new ChatEntity(Guid.NewGuid(), dto.Title);
                chat.AddParticipantUser(userId);

                await context.Chats.AddAsync(chat, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Created($"/api/chat/chats/{chat.Id}", chat);
            })
            .Produces<ChatEntity>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

            // PUT: Update a chat
            group.MapPut("/chats/{id:guid}", async (
                Guid id,
                UpdateChatDto dto,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (chat is null)
                    return Results.NotFound();

                chat.UpdateTitle(dto.Title);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok(chat);
            })
            .Produces<ChatEntity>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            // DELETE: Delete a chat
            group.MapDelete("/chats/{id:guid}", async (
                Guid id,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (chat is null)
                    return Results.NotFound();

                context.Chats.Remove(chat);
                await context.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

            // GET: Get messages for a chat
            group.MapGet("/chats/{id:guid}/messages", async (
                Guid id,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chatExists = await context.Chats
                    .AnyAsync(c => c.Id == id, cancellationToken);

                if (!chatExists)
                    return Results.NotFound();

                var messages = await context.ChatMessages
                    .Where(m => m.ChatId == id)
                    .OrderBy(m => m.PostedAt)
                    .ToListAsync(cancellationToken);

                return Results.Ok(messages);
            })
            .Produces<List<ChatMessageEntity>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            // POST: Add a message to a chat
            group.MapPost("/chats/{id:guid}/messages", async (
                Guid id,
                CreateChatMessageDto dto,
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (chat is null)
                    return Results.NotFound();

                var userId = httpContext.User.GetRequiredUserId();
                var message = new ChatMessageEntity(Guid.NewGuid(), id, dto.Content);
                message.SetSenderUser(userId);

                await context.ChatMessages.AddAsync(message, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Created($"/api/chat/chats/{id}/messages/{message.Id}", message);
            })
            .Produces<ChatMessageEntity>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status404NotFound);

            // POST: Add a participant user to a chat
            group.MapPost("/chats/{id:guid}/participants/users/{userId:int}", async (
                Guid id,
                int userId,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (chat is null)
                    return Results.NotFound();

                chat.AddParticipantUser(userId);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok();
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            // DELETE: Remove a participant user from a chat
            group.MapDelete("/chats/{id:guid}/participants/users/{userId:int}", async (
                Guid id,
                int userId,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (chat is null)
                    return Results.NotFound();

                chat.RemoveParticipantUser(userId);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok();
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            // POST: Add a participant agent to a chat
            group.MapPost("/chats/{id:guid}/participants/agents/{agentId:guid}", async (
                Guid id,
                Guid agentId,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (chat is null)
                    return Results.NotFound();

                chat.AddParticipantAgent(agentId);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok();
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            // DELETE: Remove a participant agent from a chat
            group.MapDelete("/chats/{id:guid}/participants/agents/{agentId:guid}", async (
                Guid id,
                Guid agentId,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var chat = await context.Chats
                    .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

                if (chat is null)
                    return Results.NotFound();

                chat.RemoveParticipantAgent(agentId);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok();
            })
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
        }
    }
}
