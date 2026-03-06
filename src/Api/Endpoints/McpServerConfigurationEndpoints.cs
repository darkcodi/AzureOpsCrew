using AzureOpsCrew.Api.Endpoints.Dtos.McpServerConfigurations;
using AzureOpsCrew.Api.Endpoints.Filters;
using AzureOpsCrew.Domain.McpServerConfigurations;
using AzureOpsCrew.Infrastructure.Ai.Mcp;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class McpServerConfigurationEndpoints
{
    public static void MapMcpServerConfigurationEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/mcp-server-configurations")
            .WithTags("McpServerConfigurations");
            //.RequireAuthorization();

        group.MapPost("/create", async (
            CreateMcpServerConfigurationBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var configuration = new McpServerConfiguration(
                Guid.NewGuid(),
                body.Name,
                body.Url)
            {
                Description = body.Description?.Trim(),
            };

            await context.AddAsync(configuration, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/mcp-server-configurations/{configuration.Id}", configuration.ToResponseDto());
        })
        .AddEndpointFilter<ValidationFilter<CreateMcpServerConfigurationBodyDto>>()
        .Produces<McpServerConfigurationResponseDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapPost("/{id}/set-enabled", async (
            Guid id,
            SetMcpServerConfigurationEnabledBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.McpServerConfigurations
                .Include(x => x.Tools)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            found.SetEnabled(body.IsEnabled);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found.ToResponseDto());
        })
        .AddEndpointFilter<ValidationFilter<SetMcpServerConfigurationEnabledBodyDto>>()
        .Produces<McpServerConfigurationResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/tools/set-enable", async (
            Guid id,
            SetMcpServerToolEnabledBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.McpServerConfigurations
                .Include(x => x.Tools)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            var normalizedToolName = body.Name.Trim();
            if (found.Tools.All(x => !string.Equals(x.Name, normalizedToolName, StringComparison.Ordinal)))
                return Results.NotFound();

            found.SetToolIsEnabled(normalizedToolName, body.IsEnabled);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found.ToResponseDto());
        })
        .AddEndpointFilter<ValidationFilter<SetMcpServerToolEnabledBodyDto>>()
        .Produces<McpServerConfigurationResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/set-authorization", async (
            Guid id,
            SetAuthMcpServerConfigurationBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.McpServerConfigurations
                .Include(x => x.Tools)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            found.SetAuth(body.ToDomainAuth());
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found.ToResponseDto());
        })
        .AddEndpointFilter<ValidationFilter<SetAuthMcpServerConfigurationBodyDto>>()
        .Produces<McpServerConfigurationResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/sync-tools", async (
            Guid id,
            AzureOpsCrewContext context,
            McpServerFacade mcpServerFacade,
            CancellationToken cancellationToken) =>
        {
            var found = await context.McpServerConfigurations
                .Include(x => x.Tools)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            var existingToolsByName = found.Tools
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);

            var discoveredTools = await mcpServerFacade.GetAvailableToolsAsync(found.Url, found.Auth, cancellationToken);

            foreach (var tool in discoveredTools)
            {
                if (existingToolsByName.TryGetValue(tool.Name, out var existingTool))
                    tool.IsEnabled = existingTool.IsEnabled;
            }

            found.ReplaceTools(discoveredTools, DateTime.UtcNow);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found.ToResponseDto());
        })
        .Produces<McpServerConfigurationResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id}", async (
            Guid id,
            UpdateMcpServerConfigurationBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.McpServerConfigurations
                .Include(x => x.Tools)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            found.Update(body.Name, body.Url);
            found.Description = body.Description?.Trim();

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found.ToResponseDto());
        })
        .AddEndpointFilter<ValidationFilter<UpdateMcpServerConfigurationBodyDto>>()
        .Produces<McpServerConfigurationResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.McpServerConfigurations
                .Include(x => x.Tools)
                .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

            return found is null ? Results.NotFound() : Results.Ok(found.ToResponseDto());
        })
        .Produces<McpServerConfigurationResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("", async (
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var configurations = await context.McpServerConfigurations
                .Include(x => x.Tools)
                .OrderBy(x => x.DateCreated)
                .ToListAsync(cancellationToken);

            return Results.Ok(configurations.ToResponseDtoArray());
        })
        .Produces<McpServerConfigurationResponseDto[]>(StatusCodes.Status200OK);
    }
}
