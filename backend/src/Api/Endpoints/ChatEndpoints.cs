using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class ChatEndpoints
{
    public static void MapChatEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/chats")
            .WithTags("Chats");

        // POST /api/chats - Create a new chat
        group.MapPost("", async (AzureOpsCrewContext context, CancellationToken cancellationToken) =>
        {
            var chat = new AocChat
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };

            await context.AddAsync(chat, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/chats/{chat.Id}", chat);
        })
        .Produces<AocChat>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // GET /api/chats - List all chats
        group.MapGet("", async (AzureOpsCrewContext context, CancellationToken cancellationToken) =>
        {
            var chats = await context.Set<AocChat>()
                .OrderBy(c => c.CreatedAt)
                .ToListAsync(cancellationToken);

            return Results.Ok(chats);
        })
        .Produces<AocChat[]>(StatusCodes.Status200OK);

        // GET /api/chats/{id} - Get a specific chat by ID
        group.MapGet("/{id}", async (Guid id, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
        {
            var found = await context.Set<AocChat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            return found is null ? Results.NotFound() : Results.Ok(found);
        })
        .Produces<AocChat>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/chats/{id} - Delete a chat
        group.MapDelete("/{id}", async (Guid id, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
        {
            var found = await context.Set<AocChat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (found is null)
            {
                return Results.NotFound();
            }

            context.Set<AocChat>().Remove(found);
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // GET /api/chats/{id}/messages - Get all messages for a chat
        group.MapGet("/{id}/messages", async (Guid id, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
        {
            var chat = await context.Set<AocChat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (chat is null)
            {
                return Results.NotFound();
            }

            var messages = await context.Set<AocMessage>()
                .Where(m => m.ChatId == id)
                .OrderBy(m => m.PostedAt)
                .ToListAsync(cancellationToken);

            return Results.Ok(messages);
        })
        .Produces<AocMessage[]>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST /api/chats/{id}/messages - Post a message to a chat
        group.MapPost("/{id}/messages", async (Guid id, CreateMessageDto body, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
        {
            var chat = await context.Set<AocChat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (chat is null)
            {
                return Results.NotFound();
            }

            var message = new AocMessage
            {
                Id = Guid.NewGuid(),
                ChatId = id,
                AuthorName = body.AuthorName,
                Text = body.Text,
                PostedAt = DateTime.UtcNow
            };

            await context.AddAsync(message, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/chats/{id}/messages/{message.Id}", message);
        })
        .Produces<AocMessage>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        // DELETE /api/chats/{id}/messages/{messageId} - Delete a message
        group.MapDelete("/{id}/messages/{messageId}", async (Guid id, Guid messageId, AzureOpsCrewContext context, CancellationToken cancellationToken) =>
        {
            var chat = await context.Set<AocChat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (chat is null)
            {
                return Results.NotFound();
            }

            var message = await context.Set<AocMessage>()
                .SingleOrDefaultAsync(m => m.Id == messageId && m.ChatId == id, cancellationToken);

            if (message is null)
            {
                return Results.NotFound();
            }

            context.Set<AocMessage>().Remove(message);
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);
    }
}
