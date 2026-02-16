using AzureOpsCrew.Api.Endpoints.Dtos.Providers;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class ProviderConfigEndpoints
{
    public static void MapProviderConfigEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/providers")
            .WithTags("Providers");

        // CREATE
        group.MapPost("/create", async (
            CreateProviderConfigBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var config = new ProviderConfig(
                Guid.NewGuid(),
                body.ClientId,
                body.Name,
                body.ProviderType,
                body.ApiKey,
                body.ApiEndpoint,
                body.DefaultModel);

            await context.AddAsync(config, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/providers/{config.Id}", config);
        })
        .Produces<ProviderConfig>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // LIST (by client)
        group.MapGet("", async (
            int clientId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var configs = await context.Set<ProviderConfig>()
                .Where(p => p.ClientId == clientId)
                .OrderBy(p => p.ProviderType)
                .ThenBy(p => p.Name)
                .ToListAsync(cancellationToken);

            return Results.Ok(configs);
        })
        .Produces<ProviderConfig[]>(StatusCodes.Status200OK);

        // GET by ID
        group.MapGet("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.Set<ProviderConfig>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            return found is null ? Results.NotFound() : Results.Ok(found);
        })
        .Produces<ProviderConfig>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // UPDATE
        group.MapPut("/{id}", async (
            Guid id,
            UpdateProviderConfigBodyDto body,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.Set<ProviderConfig>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            found.Update(body.Name, body.ApiKey, body.ApiEndpoint, body.DefaultModel);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found);
        })
        .Produces<ProviderConfig>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);

        // DELETE
        group.MapDelete("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.Set<ProviderConfig>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            context.Set<ProviderConfig>().Remove(found);
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        // TEST CONNECTION
        group.MapPost("/{id}/test", async (
            Guid id,
            IProviderServiceFactory providerServiceFactory,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var config = await context.Set<ProviderConfig>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (config is null)
                return Results.NotFound();

            var service = providerServiceFactory.GetService(config.ProviderType);
            var success = await service.TestConnectionAsync(config, cancellationToken);

            return Results.Ok(new TestConnectionResponseDto(
                success,
                success ? "Connection successful" : "Connection failed"));
        })
        .Produces<TestConnectionResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // LIST MODELS
        group.MapGet("/{id}/models", async (
            Guid id,
            IProviderServiceFactory providerServiceFactory,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var config = await context.Set<ProviderConfig>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (config is null)
                return Results.NotFound();

            var service = providerServiceFactory.GetService(config.ProviderType);
            var models = await service.ListModelsAsync(config, cancellationToken);

            return Results.Ok(models);
        })
        .Produces<ProviderModelInfo[]>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
