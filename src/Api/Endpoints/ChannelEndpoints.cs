using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Background;
using AzureOpsCrew.Api.Endpoints.Dtos.Agents;
using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Api.Endpoints.Dtos.Chats;
using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Infrastructure.Ai.Models;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using System.Text.Json;

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
                AgentIds = body.AgentIds.ToArray()
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

            var agentId = body.AgentId;
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

            channel.RemoveAgent(body.AgentId);

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

        // GET: /api/channels/{channelId}/approvals - Returns all approval requests for a channel with their status
        group.MapGet("/{channelId}/approvals", async (
            Guid channelId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == channelId, cancellationToken);

            if (channel is null)
                return Results.NotFound();

            var approvalThoughts = await context.AgentThoughts
                .Where(t => t.ThreadId == channelId
                    && (t.ContentType == LlmMessageContentType.FunctionApprovalRequestContent
                        || t.ContentType == LlmMessageContentType.FunctionApprovalResponseContent))
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var responsesById = new Dictionary<string, AocFunctionApprovalResponseContent>();
            var agentIds = approvalThoughts.Select(t => t.AgentId).Distinct().ToList();
            var agentNamesById = await context.Agents
                .Where(a => agentIds.Contains(a.Id))
                .Select(a => new { a.Id, a.Info.Username })
                .ToDictionaryAsync(a => a.Id, a => a.Username, cancellationToken);
            foreach (var thought in approvalThoughts)
            {
                if (thought.ContentType == LlmMessageContentType.FunctionApprovalResponseContent)
                {
                    var resp = JsonSerializer.Deserialize<AocFunctionApprovalResponseContent>(thought.ContentJson);
                    if (resp != null)
                        responsesById[resp.Id] = resp;
                }
            }

            var requests = new List<object>();
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

        // GET: /api/channels/{channelId}/agents/{agentId}/mind - Returns agent thoughts scoped to a specific channel
        group.MapGet("/{channelId}/agents/{agentId}/mind", async (
            Guid channelId,
            Guid agentId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            // Verify channel exists
            var channel = await context.Channels
                .SingleOrDefaultAsync(c => c.Id == channelId, cancellationToken);

            if (channel is null)
                return Results.NotFound();

            // Filter by both AgentId and ThreadId (which equals channelId)
            var messages = await context.AgentThoughts
                .Where(m => m.AgentId == agentId && m.ThreadId == channelId && !m.IsHidden)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);

            var historyMessages = new List<AgentMindEventDto>();

            // First pass: collect tool result content by CallId (from tool-role messages)
            var toolResultsByCallId = new Dictionary<(Guid threadId, string callId), string>();
            //var toolResultsByCallId = new Dictionary<(Guid threadId, Guid runId, string callId), string>();
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
                    //toolResultsByCallId[(msg.ThreadId, msg.RunId, functionResult.CallId)] = resultStr ?? "";
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
                         //&& toolResultsByCallId.TryGetValue((msg.ThreadId, msg.RunId, functionCallContent.CallId), out var resultStr))
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

        // POST: /api/channels/{channelId}/approvals/{approvalId} - Responds to an approval request in a channel
        group.MapPost("/{channelId}/approvals/{approvalId}", async (
            Guid channelId,
            string approvalId,
            ApprovalResponseDto body,
            AzureOpsCrewContext context,
            AgentTriggerQueue agentTriggerQueue,
            CancellationToken cancellationToken) =>
        {
            var channel = await context.Set<Channel>()
                .SingleOrDefaultAsync(c => c.Id == channelId, cancellationToken);

            if (channel is null)
                return Results.NotFound("Channel not found.");

            // Find the approval request thought in this channel
            var requestThoughts = await context.AgentThoughts
                .Where(t => t.ThreadId == channelId
                    && t.ContentType == LlmMessageContentType.FunctionApprovalRequestContent)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var matchingThought = requestThoughts.FirstOrDefault(t =>
            {
                var content = JsonSerializer.Deserialize<AocFunctionApprovalRequestContent>(t.ContentJson);
                return content?.Id == approvalId;
            });

            if (matchingThought is null)
                return Results.NotFound("Approval request not found.");

            var existingResponses = await context.AgentThoughts
                .Where(t => t.AgentId == matchingThought.AgentId
                    && t.ThreadId == channelId
                    && t.ContentType == LlmMessageContentType.FunctionApprovalResponseContent)
                .ToListAsync(cancellationToken);

            var existingResponse = existingResponses.Any(t =>
                JsonSerializer.Deserialize<AocFunctionApprovalResponseContent>(t.ContentJson)?.Id == approvalId);

            if (existingResponse)
                return Results.Conflict("Approval request already has a response.");

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
            var domainThought = responseThought.ToDomain(matchingThought.AgentId, channelId, matchingThought.RunId);
            context.AgentThoughts.Add(domainThought);
            await context.SaveChangesAsync(cancellationToken);

            // Re-enqueue the agent to continue processing
            agentTriggerQueue.Enqueue(matchingThought.AgentId, channelId);

            return Results.Ok(new { approved = body.Approved });
        })
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

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
            foreach (var agentId in channel.AgentIds)
            {
                agentTriggerQueue.Enqueue(agentId, channel.Id);
            }

            return Results.Created($"/api/channels/{id}/messages/{message.Id}", message);
        })
        .Produces<Message>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status404NotFound);
    }
}


