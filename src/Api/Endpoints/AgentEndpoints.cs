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

                var userChannels = await context.Set<Channel>()
                    .ToListAsync(cancellationToken);

                var channelsWithAgent = userChannels
                    .Where(c => c.AgentIds.Contains(found.Id))
                    .ToList();

                foreach (var channel in channelsWithAgent)
                    channel.RemoveAgent(found.Id);

                context.Set<Agent>().Remove(found);
                await context.SaveChangesAsync(cancellationToken);

                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

            group.MapPost("/{id}/set-available-mcp-server", async (
                Guid id,
                SetAvailableMcpServerBodyDto body,
                AzureOpsCrewContext context,
                CancellationToken cancellationToken) =>
            {
                var found = await context.Set<Agent>()
                    .SingleOrDefaultAsync(a => a.Id == id, cancellationToken);

                if (found is null)
                    return Results.NotFound();

                var enabledToolNames = body.EnabledToolNames ?? [];

                if (enabledToolNames.Length == 0)
                {
                    found.RemoveAvailableMcpServer(body.McpServerConfigurationId);
                }
                else
                {
                    var availability = new AgentMcpServerToolAvailability(body.McpServerConfigurationId)
                    {
                        EnabledToolNames = enabledToolNames,
                    };

                    found.SetAvailableMcpServer(availability);
                }

                await context.SaveChangesAsync(cancellationToken);

                return Results.Ok(found);
            })
            .Produces<Agent>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        }
    }
}
