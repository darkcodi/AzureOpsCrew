using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Domain.Agents;
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
                AgentIds = body.AgentIds.Select(a => a.ToString("D")).ToArray()
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
                .OrderBy(c => c.DateCreated)
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
    }
}