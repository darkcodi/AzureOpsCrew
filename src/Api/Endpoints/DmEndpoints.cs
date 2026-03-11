using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Background;
using AzureOpsCrew.Api.Endpoints.Dtos.Agents;
using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using System.Text.Json;
using AzureOpsCrew.Domain.Triggers;

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

        // GET: /api/dms/{dmId}/approvals - Returns all approval requests for a DM with their status
        group.MapGet("/{dmId}/approvals", async (
            HttpContext httpContext,
            Guid dmId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();
            var dmExists = await context.Dms.AnyAsync(dm => dm.Id == dmId && (dm.User1Id == userId || dm.User2Id == userId), cancellationToken);
            if (!dmExists)
                return Results.NotFound();

            // Load all approval-related thoughts for this thread
            var approvalThoughts = await context.AgentThoughts
                .Where(t => t.ThreadId == dmId
                    && (t.ContentType == LlmMessageContentType.FunctionApprovalRequestContent
                        || t.ContentType == LlmMessageContentType.FunctionApprovalResponseContent))
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            // Build response: pair requests with their responses
            var requests = new List<object>();
            var responsesById = new Dictionary<string, AocFunctionApprovalResponseContent>();
            var agentIds = approvalThoughts.Select(t => t.AgentId).Distinct().ToList();
            var agentNamesById = await context.Agents
                .Where(a => agentIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Info.Username })
                .ToDictionaryAsync(a => a.Id, a => a.Username, cancellationToken);

            // First pass: collect responses
            foreach (var thought in approvalThoughts)
            {
                if (thought.ContentType == LlmMessageContentType.FunctionApprovalResponseContent)
                {
                    var resp = JsonSerializer.Deserialize<AocFunctionApprovalResponseContent>(thought.ContentJson);
                    if (resp != null)
                        responsesById[resp.Id] = resp;
                }
            }

            // Second pass: build request list with status
            foreach (var thought in approvalThoughts)
            {
                if (thought.ContentType != LlmMessageContentType.FunctionApprovalRequestContent)
                    continue;

                var req = JsonSerializer.Deserialize<AocFunctionApprovalRequestContent>(thought.ContentJson);
                if (req == null) continue;

                var hasResponse = responsesById.TryGetValue(req.Id, out var response);
                var status = hasResponse ? (response!.Approved ? "approved" : "rejected") : "pending";

                requests.Add(new
                {
                    approvalId = req.Id,
                    toolName = req.FunctionCall?.Name,
                    callId = req.FunctionCall?.CallId,
                    args = req.FunctionCall?.Arguments,
                    agentId = thought.AgentId,
                    agentName = agentNamesById.TryGetValue(thought.AgentId, out var agentName) ? agentName : null,
                    serverName = req.ServerName,
                    timestamp = new DateTimeOffset(thought.CreatedAt, TimeSpan.Zero),
                    status,
                    reason = hasResponse ? response!.Reason : null,
                });
            }

            return Results.Ok(requests);
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

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
            var toolResultsByCallId = new Dictionary<(Guid threadId, string callId), string>();
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
                    toolResultsByCallId[(msg.ThreadId, functionResult.CallId)] = resultStr ?? "";
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
                         && toolResultsByCallId.TryGetValue((msg.ThreadId, functionCallContent.CallId), out var resultStr))
                {
                    object? resultObj;
                    try
                    {
                        var deserialized = JsonSerializer.Deserialize<JsonElement>(resultStr);

                        // The result may be wrapped in a ToolCallResult object: { "CallId": "...", "Result": "...", "IsError": false }
                        // Extract the actual Result value if this wrapper is present
                        if (deserialized.ValueKind == JsonValueKind.Object &&
                            deserialized.TryGetProperty("Result", out var actualResult))
                        {
                            // Check if actualResult is a JSON string that needs to be parsed
                            if (actualResult.ValueKind == JsonValueKind.String)
                            {
                                var innerStr = actualResult.GetString();
                                if (!string.IsNullOrEmpty(innerStr))
                                {
                                    try
                                    {
                                        resultObj = JsonSerializer.Deserialize<JsonElement>(innerStr);
                                    }
                                    catch
                                    {
                                        resultObj = actualResult;
                                    }
                                }
                                else
                                {
                                    resultObj = actualResult;
                                }
                            }
                            else
                            {
                                resultObj = actualResult;
                            }
                        }
                        else
                        {
                            resultObj = deserialized;
                        }
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
            AgentScheduler agentScheduler,
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

            // Enqueue the agent to process the new message
            var trigger = new MessageTrigger
            {
                Id = Guid.NewGuid(),
                AgentId = agentId,
                ChatId = dm.Id,
                CreatedAt = DateTime.UtcNow,
                MessageId = message.Id,
                AuthorId = userId,
                AuthorName = user?.Username ?? "Unknown",
                MessageContent = dto.Content,
            };
            await agentScheduler.QueueTrigger(Trigger.FromSpecificTrigger(trigger));

            return Results.Created($"/api/users/{userId}/dms/agents/{agentId}/messages/{message.Id}", message);
        })
        .Produces<Message>(StatusCodes.Status201Created);

        // POST: /api/dms/agents/{agentId}/approvals/{approvalId} - Responds to an approval request
        group.MapPost("/agents/{agentId}/approvals/{approvalId}", async (
            HttpContext httpContext,
            Guid agentId,
            string approvalId,
            ApprovalResponseDto body,
            AzureOpsCrewContext context,
            AgentScheduler agentScheduler,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();

            // Find the DM channel between the user and the agent
            var dm = await context.Dms
                .FirstOrDefaultAsync(dm =>
                    (dm.User1Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User1Id == userId && dm.Agent2Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent2Id == agentId),
                    cancellationToken);

            if (dm is null)
                return Results.NotFound("DM channel not found.");

            // Find the approval request thought
            var requestThought = await context.AgentThoughts
                .Where(t => t.AgentId == agentId
                    && t.ThreadId == dm.Id
                    && t.ContentType == LlmMessageContentType.FunctionApprovalRequestContent)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var matchingThought = requestThought.FirstOrDefault(t =>
            {
                var content = JsonSerializer.Deserialize<AocFunctionApprovalRequestContent>(t.ContentJson);
                return content?.Id == approvalId;
            });

            if (matchingThought is null)
                return Results.NotFound("Approval request not found.");

            var existingResponses = await context.AgentThoughts
                .Where(t => t.AgentId == agentId
                    && t.ThreadId == dm.Id
                    && t.ContentType == LlmMessageContentType.FunctionApprovalResponseContent)
                .ToListAsync(cancellationToken);

            var existingResponse = existingResponses.Any(t =>
                JsonSerializer.Deserialize<AocFunctionApprovalResponseContent>(t.ContentJson)?.Id == approvalId);

            if (existingResponse)
                return Results.Conflict("Approval request already has a response.");

            // Deserialize the original function call from the request
            var requestContent = JsonSerializer.Deserialize<AocFunctionApprovalRequestContent>(matchingThought.ContentJson);

            // Save the approval response as an agent thought
            var responseContent = new AocFunctionApprovalResponseContent
            {
                Id = approvalId,
                Approved = body.Approved,
                Reason = body.Reason,
                FunctionCall = requestContent?.FunctionCall
            };
            var responseThought = AocAgentThought.FromContent(
                responseContent, ChatRole.User, null, DateTime.UtcNow, Guid.NewGuid());
            var domainThought = responseThought.ToDomain(agentId, dm.Id, matchingThought.RunId);
            context.AgentThoughts.Add(domainThought);
            await context.SaveChangesAsync(cancellationToken);

            // Re-enqueue the agent to continue processing
            await agentScheduler.QueueTrigger(Trigger.FromSpecificTrigger(new ToolApprovalTrigger
            {
                Id = Guid.NewGuid(),
                AgentId = matchingThought.AgentId,
                ChatId = dm.Id,
                CreatedAt = DateTime.UtcNow,
                CallId = requestContent?.FunctionCall?.CallId ?? "",
                ToolName = requestContent?.FunctionCall?.Name ?? "",
                Parameters = requestContent != null
                    ? JsonSerializer.Serialize(requestContent.FunctionCall?.Arguments ?? new Dictionary<string, object?>())
                    : ""
            }));

            return Results.Ok(new { approved = body.Approved });
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // POST: /api/dms/agents/{agentId}/stop - Stops a running agent in a DM
        group.MapPost("/agents/{agentId}/stop", (
            HttpContext httpContext,
            Guid agentId,
            AzureOpsCrewContext context,
            AgentScheduler agentScheduler,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.GetRequiredUserId();

            // Find the DM channel between the user and the agent
            var dm = context.Dms
                .FirstOrDefault(dm =>
                    (dm.User1Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent1Id == agentId) ||
                    (dm.User1Id == userId && dm.Agent2Id == agentId) ||
                    (dm.User2Id == userId && dm.Agent2Id == agentId));

            if (dm is null)
                return Results.NotFound("DM channel not found.");

            var stopped = agentScheduler.StopAgent(agentId, dm.Id);
            return stopped
                ? Results.Ok(new { stopped = true })
                : Results.Ok(new { stopped = false, message = "Agent was not running." });
        })
        .Produces(StatusCodes.Status200OK);
    }
}



