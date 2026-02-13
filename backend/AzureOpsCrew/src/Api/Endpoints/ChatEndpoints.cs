using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.ChatProcessings;
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

        group.MapPost("/create", async (
            CreateChatBodyDto body,
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

            var chat = new Chat(Guid.NewGuid(), body.ClientId, body.Name)
            {
                Description = body.Description,
                AgentIds = body.AgentIds.Select(x => x).ToArray()
            };

            await context.AddAsync(chat, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // draft output: guid
            return Results.Created($"/api/chats/{chat.Id}", new { chatId = chat.Id });
        })
        .Produces(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("", async (
            int clientId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var chats = await context.Set<Chat>()
                .Where(c => c.ClientId == clientId)
                .OrderByDescending(c => c.DateCreated)
                .ToListAsync(cancellationToken);

            return Results.Ok(chats);
        })
        .Produces<Chat[]>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var chat = await context.Set<Chat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            return chat is null ? Results.NotFound() : Results.Ok(chat);
        })
        .Produces<Chat>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/messages/send", async (
            Guid id,
            SendChatMessageBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var chat = await context.Set<Chat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (chat is null)
                return Results.NotFound();

            if (chat.ClientId != body.ClientId)
                return Results.BadRequest("ClientId does not match chat owner.");

            chat.AddMessage(body.Text.Trim(), MessageSender.Client());

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok();
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        //This endpoint emulates worker chat processing on event bus message
        group.MapPost("/{id}/messages/test-emulate-worker-processing", async (
           Guid id,
           AzureOpsCrewContext context,
           IChatProcessor chatProcessor,
           CancellationToken cancellationToken) =>
        {
            var chat = await context.Set<Chat>()
                .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

            if (chat is null)
                return Results.NotFound();

            await chatProcessor.Process(chat, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok();
        })
           .Produces(StatusCodes.Status200OK)
           .Produces(StatusCodes.Status400BadRequest)
           .Produces(StatusCodes.Status404NotFound);
    }
}