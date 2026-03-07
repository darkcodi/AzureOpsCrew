using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Background;
using AzureOpsCrew.Api.Endpoints.Dtos.Agents;
using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace AzureOpsCrew.Api.Endpoints;

public static class DmEndpoints
{
    public static void MapDmEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/dms")
            .WithTags("DirectMessages")
            .RequireAuthorization();

        // GET: /api/dms - Returns all DM channels where the specified user is a participant
        group.MapGet("", async (
            HttpContext httpContext,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var dms = await context.Dms
                .Where(dm => dm.User1Id == userId
                          || dm.User2Id == userId)
                .OrderBy(dm => dm.CreatedAt)
                .ToListAsync(cancellationToken);

            return Results.Ok(dms);
        })
        .Produces<DirectMessageChannel[]>(StatusCodes.Status200OK);

        // GET: /api/dms/users/{otherUserId}/messages - Returns messages between two users
        group.MapGet("/users/{otherUserId}/messages", async (
            HttpContext httpContext,
            Guid otherUserId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.User2Id == otherUserId) ||
                    (dm.User1Id == otherUserId && dm.User2Id == userId),
                    cancellationToken);

            if (dm is null)
                return Results.Ok(new List<Message>());

            var messages = await context.Messages
                .Where(m => m.DmId == dm.Id)
                .OrderBy(m => m.PostedAt)
                .ToListAsync(cancellationToken);
            return Results.Ok(messages);
        })
        .Produces<List<Message>>(StatusCodes.Status200OK);

        // GET: /api/dms/agents/{agentId}/messages - Returns messages between a user and an agent
        group.MapGet("/agents/{agentId}/messages", async (
            HttpContext httpContext,
            Guid agentId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User1Id == userId && dm.Agent2Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent2Id == agentId),
                    cancellationToken);

            if (dm is null)
                return Results.Ok(new List<Message>());

            var messages = await context.Messages
                .Where(m => m.DmId == dm.Id)
                .OrderBy(m => m.PostedAt)
                .ToListAsync(cancellationToken);
            return Results.Ok(messages);
        })
        .Produces<List<Message>>(StatusCodes.Status200OK);

        // GET: /api/dms/{dmId}/agents/{agentId}/mind - Returns agent thoughts scoped to a specific DM
        group.MapGet("/{dmId}/agents/{agentId}/mind", async (
            Guid dmId,
            Guid agentId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            // Verify DM exists
            var dmExists = await context.Dms
                .AnyAsync(dm => dm.Id == dmId, cancellationToken);

            if (!dmExists)
                return Results.NotFound();

            // Filter by both AgentId and ThreadId (which equals dmId)
            var messages = await context.AgentThoughts
                .Where(m => m.AgentId == agentId && m.ThreadId == dmId && !m.IsHidden)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);

            var historyMessages = new List<AgentMindEventDto>();

            // First pass: collect tool result content by CallId (from tool-role messages)
            var toolResultsByCallId = new Dictionary<(Guid threadId, Guid runId, string callId), string>();
            foreach (var msg in messages)
            {
                if (msg.Role.ToString() != "tool")
                    continue;
                var contentDto = new AocAiContentDto
                {
                    Content = msg.ContentJson,
                    ContentType = msg.ContentType
                };
                var aiContent = contentDto.ToAocAiContent();
                if (aiContent is AocFunctionResultContent functionResult)
                {
                    var resultStr = functionResult.Result switch
                    {
                        null => "<null>",
                        string s => s,
                        JsonElement el => el.GetRawText(),
                        _ => JsonSerializer.Serialize(functionResult.Result)
                    };
                    toolResultsByCallId[(msg.ThreadId, msg.RunId, functionResult.CallId)] = resultStr ?? "";
                }
            }

            foreach (var msg in messages)
            {
                // Skip tool role messages (internal function calls)
                if (msg.Role.ToString() == "tool")
                    continue;

                // Deserialize content
                var contentDto = new AocAiContentDto
                {
                    Content = msg.ContentJson,
                    ContentType = msg.ContentType
                };
                var aiContent = contentDto.ToAocAiContent();

                if (aiContent is AocTextContent textContent)
                {
                    historyMessages.Add(new AgentMindEventDto
                    {
                        Id = msg.Id.ToString(),
                        Role = msg.Role.ToString(),
                        Content = textContent.Text,
                        Timestamp = new DateTimeOffset(msg.CreatedAt, TimeSpan.Zero),
                    });
                }
                else if (aiContent is AocTextReasoningContent reasoningContent)
                {
                    historyMessages.Add(new AgentMindEventDto
                    {
                        Id = msg.Id.ToString(),
                        Role = msg.Role.ToString(),
                        Content = null,
                        Reasoning = reasoningContent.Text,
                        Timestamp = new DateTimeOffset(msg.CreatedAt, TimeSpan.Zero),
                    });
                }
                else if (aiContent is AocFunctionCallContent functionCallContent
                         && toolResultsByCallId.TryGetValue((msg.ThreadId, msg.RunId, functionCallContent.CallId), out var resultStr))
                {
                    object? resultObj;
                    try
                    {
                        resultObj = JsonSerializer.Deserialize<JsonElement>(resultStr);
                    }
                    catch
                    {
                        resultObj = new Dictionary<string, object?> { ["raw"] = resultStr };
                    }

                    historyMessages.Add(new AgentMindEventDto
                    {
                        Id = functionCallContent.CallId,
                        Role = msg.Role.ToString(),
                        Content = "",
                        Timestamp = new DateTimeOffset(msg.CreatedAt, TimeSpan.Zero),
                        Widget = new UiWidgetDto
                        {
                            ToolName = functionCallContent.Name,
                            CallId = functionCallContent.CallId,
                            Args = functionCallContent.Arguments ?? new Dictionary<string, object?>(),
                            Result = resultObj
                        }
                    });
                }
            }

            return Results.Ok(new AgentMindResponseDto { Events = historyMessages });
        })
        .Produces<AgentMindResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST: /api/dms/agents/{agentId}/ensure-channel - Returns or creates a DM channel between the user and an agent
        group.MapPost("/agents/{agentId}/ensure-channel", async (
            HttpContext httpContext,
            Guid agentId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User1Id == userId && dm.Agent2Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent2Id == agentId),
                    cancellationToken);

            if (dm is null)
            {
                dm = new DirectMessageChannel
                {
                    Id = Guid.NewGuid(),
                    User1Id = userId,
                    Agent1Id = agentId,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Dms.AddAsync(dm, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            return Results.Ok(dm);
        })
        .Produces<DirectMessageChannel>(StatusCodes.Status200OK);

        // POST: /api/dms/users/{otherUserId}/messages - Posts a message between two users
        group.MapPost("/users/{otherUserId}/messages", async (
            HttpContext httpContext,
            Guid otherUserId,
            CreateDirectMessageDto dto,
            AzureOpsCrewContext context,
            IChannelEventBroadcaster channelEventBroadcaster,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.User2Id == otherUserId) ||
                    (dm.User1Id == otherUserId && dm.User2Id == userId),
                    cancellationToken);

            // Create DM channel if it doesn't exist
            if (dm is null)
            {
                dm = new DirectMessageChannel
                {
                    Id = Guid.NewGuid(),
                    User1Id = userId,
                    User2Id = otherUserId,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Dms.AddAsync(dm, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
            var message = new Message
            {
                Id = Guid.NewGuid(),
                Text = dto.Content,
                PostedAt = DateTime.UtcNow,
                UserId = userId,
                DmId = dm.Id,
                AuthorName = user?.Username,
            };
            await context.Messages.AddAsync(message, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // Broadcast the new message via SignalR
            await channelEventBroadcaster.BroadcastDmMessageAddedAsync(dm.Id, message);

            return Results.Created($"/api/users/{userId}/dms/users/{otherUserId}/messages/{message.Id}", message);
        })
        .Produces<Message>(StatusCodes.Status201Created);

        // POST: /api/dms/agents/{agentId}/messages - Posts a message between a user and an agent
        group.MapPost("/agents/{agentId}/messages", async (
            HttpContext httpContext,
            Guid agentId,
            CreateDirectMessageDto dto,
            AzureOpsCrewContext context,
            AgentTriggerQueue agentTriggerQueue,
            IChannelEventBroadcaster channelEventBroadcaster,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
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
                dm = new DirectMessageChannel
                {
                    Id = Guid.NewGuid(),
                    User1Id = userId,
                    Agent1Id = agentId,
                    CreatedAt = DateTime.UtcNow
                };
                await context.Dms.AddAsync(dm, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);
            }

            var user = await context.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
            var message = new Message
            {
                Id = Guid.NewGuid(),
                Text = dto.Content,
                PostedAt = DateTime.UtcNow,
                UserId = userId,
                DmId = dm.Id,
                AuthorName = user?.Username,
            };
            await context.Messages.AddAsync(message, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            // Broadcast the new message via SignalR
            await channelEventBroadcaster.BroadcastDmMessageAddedAsync(dm.Id, message);

            agentTriggerQueue.Enqueue(agentId, dm.Id);

            return Results.Created($"/api/users/{userId}/dms/agents/{agentId}/messages/{message.Id}", message);
        })
        .Produces<Message>(StatusCodes.Status201Created);
    }
}
