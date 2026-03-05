using System.Text.Json;
using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.Agents;
using AzureOpsCrew.Api.Endpoints.Filters;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Channels;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints
{
    public static class AgentEndpoints
    {
        public static void MapAgentEndpoints(this IEndpointRouteBuilder routeBuilder)
        {
            var group = routeBuilder.MapGroup("/api/agents")
                .WithTags("Agents")
                .RequireAuthorization();

            group.MapPost("/create", async (
                CreateAgentBodyDto body,
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var userId = httpContext.User.GetRequiredUserId();

                var normalizedUsername = body.Username.Trim().ToLowerInvariant();

                // Check username uniqueness across both Users and Agents
                var userUsernameExists = await context.Users
                    .AnyAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);

                if (userUsernameExists)
                    return Results.Conflict(new { error = "Username is already taken." });

                var agentUsernameExists = await context.Agents
                    .AnyAsync(a => a.Info.Username == normalizedUsername, cancellationToken);

                if (agentUsernameExists)
                    return Results.Conflict(new { error = "Username is already taken." });

                var providerExists = await context.Providers
                    .AnyAsync(p => p.Id == body.ProviderId, cancellationToken);

                if (!providerExists)
                    return Results.BadRequest("Provider not found.");

                var providerAgentId = Guid.NewGuid().ToString("D");
                var agentInfo = body.ToAgentInfo();

                var agent = new Agent(
                    Guid.NewGuid(),
                    agentInfo,
                    body.ProviderId,
                    providerAgentId,
                    body.Color
                );

                await context.AddAsync(agent, cancellationToken);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Created($"/api/agents/{agent.Id}", agent);
            })
            .AddEndpointFilter<ValidationFilter<CreateAgentBodyDto>>()
            .Produces<Agent>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

            group.MapGet("", async (
                HttpContext httpContext,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var agents = await context.Set<Agent>()
                    .OrderBy(a => a.DateCreated)
                    .ToListAsync(cancellationToken);

                return Results.Ok(agents);
            })
            .Produces<Agent[]>(StatusCodes.Status200OK);

            group.MapGet("/{id}", async (
                Guid id,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

                return found is null ? Results.NotFound() : Results.Ok(found);
            })
            .Produces<Agent>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

            group.MapPut("/{id}", async (
                Guid id,
                UpdateAgentBodyDto body,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

                if (found is null)
                    return Results.NotFound();

                var normalizedUsername = body.Username.Trim().ToLowerInvariant();

                // Check username uniqueness if it's being changed
                if (found.Info.Username.ToLowerInvariant() != normalizedUsername)
                {
                    var userUsernameExists = await context.Users
                        .AnyAsync(u => u.NormalizedUsername == normalizedUsername, cancellationToken);

                    if (userUsernameExists)
                        return Results.Conflict(new { error = "Username is already taken." });

                    var agentUsernameExists = await context.Agents
                        .AnyAsync(a => a.Id != id && a.Info.Username == normalizedUsername, cancellationToken);

                    if (agentUsernameExists)
                        return Results.Conflict(new { error = "Username is already taken." });
                }

                var providerExists = await context.Providers
                    .AnyAsync(p => p.Id == body.ProviderId, cancellationToken);

                if (!providerExists)
                    return Results.BadRequest("Provider not found.");

                var agentInfo = body.ToAgentInfo();
                found.Update(agentInfo, body.ProviderId, body.Color);
                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok(found);
            })
            .AddEndpointFilter<ValidationFilter<UpdateAgentBodyDto>>()
            .Produces<Agent>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

            group.MapDelete("/{id}", async (
                Guid id,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

                if (found is null)
                    return Results.NotFound();

                var agentIdString = found.Id.ToString();

                var userChannels = await context.Set<Channel>()
                    .ToListAsync(cancellationToken);

                var channelsWithAgent = userChannels
                    .Where(c => c.AgentIds.Contains(agentIdString))
                    .ToList();

                foreach (var channel in channelsWithAgent)
                    channel.RemoveAgent(agentIdString);

                context.Set<Agent>().Remove(found);
                await context.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

            group.MapGet("/{id}/mind", async (
                Guid id,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var messages = await context.AgentThoughts
                    .Where(m => m.AgentId == id && !m.IsHidden)
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
                            Timestamp = msg.CreatedAt
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
                            Timestamp = msg.CreatedAt
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
                            Timestamp = msg.CreatedAt,
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
            .Produces<AgentMindResponseDto>(StatusCodes.Status200OK);
        }
    }
}
