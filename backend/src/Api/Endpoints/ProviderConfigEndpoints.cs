using AzureOpsCrew.Api.Endpoints.Dtos.Providers;
using AzureOpsCrew.Api.Endpoints.Filters;
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
            IProviderServiceFactory providerServiceFactory,
            CancellationToken cancellationToken) =>
        {
            var config = new ProviderConfig(
                Guid.NewGuid(),
                body.ClientId,
                body.Name,
                body.ProviderType,
                body.ApiKey,
                body.ApiEndpoint,
                body.DefaultModel,
                body.IsEnabled,
                body.SelectedModels);

            // Test connection before saving
            var service = providerServiceFactory.GetService(config.ProviderType);
            var testResult = await service.TestConnectionAsync(config, cancellationToken);
            if (!testResult.Success)
            {
                return Results.BadRequest(new
                {
                    Error = "Connection test failed",
                    TestResult = new
                    {
                        Success = false,
                        Message = testResult.ErrorDetails,
                        ErrorType = testResult.ErrorType
                    }
                });
            }

            // Set models count from test result
            if (testResult.AvailableModels != null)
            {
                config.SetModelsCount(testResult.AvailableModels.Length);
            }

            await context.AddAsync(config, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/providers/{config.Id}", config);
        })
        .AddEndpointFilter<ValidationFilter<CreateProviderConfigBodyDto>>()
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
                .OrderBy(p => p.DateCreated)
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
            IProviderServiceFactory providerServiceFactory,
            CancellationToken cancellationToken) =>
        {
            var found = await context.Set<ProviderConfig>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            found.Update(body.Name, body.ApiKey, body.ApiEndpoint, body.DefaultModel, body.IsEnabled, body.SelectedModels);

            // Test connection before saving
            var service = providerServiceFactory.GetService(found.ProviderType);
            var testResult = await service.TestConnectionAsync(found, cancellationToken);
            if (!testResult.Success)
            {
                return Results.BadRequest(new
                {
                    Error = "Connection test failed",
                    TestResult = new
                    {
                        Success = false,
                        Message = testResult.ErrorDetails,
                        ErrorType = testResult.ErrorType
                    }
                });
            }

            // Set models count from test result
            if (testResult.AvailableModels != null)
            {
                found.SetModelsCount(testResult.AvailableModels.Length);
            }

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found);
        })
        .AddEndpointFilter<ValidationFilter<UpdateProviderConfigBodyDto>>()
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

        // TEST CONNECTION (by inline config, for drafts / not-yet-saved providers) — must be before /{id}/test
        group.MapPost("/test", async (
            TestConnectionBodyDto body,
            IProviderServiceFactory providerServiceFactory,
            CancellationToken cancellationToken) =>
        {
            var config = new ProviderConfig(
                Guid.Empty,
                0,
                body.Name ?? "Test",
                body.ProviderType,
                body.ApiKey,
                body.ApiEndpoint,
                body.DefaultModel,
                true);
            var service = providerServiceFactory.GetService(config.ProviderType);
            var result = await service.TestConnectionAsync(config, cancellationToken);

            return Results.Ok(new TestConnectionResponseDto(
                result.Success,
                result.Success ? "Connection successful" : result.ErrorDetails,
                result.ErrorType,
                result.LatencyMs,
                result.CheckedAt,
                result.Quota,
                result.AvailableModels?.Select(m => new ModelInfoDto(m.Id, m.Name, m.ContextSize)).ToArray()));
        })
        .AddEndpointFilter<ValidationFilter<TestConnectionBodyDto>>()
        .Produces<TestConnectionResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // TEST CONNECTION (by saved provider id)
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
            var result = await service.TestConnectionAsync(config, cancellationToken);

            return Results.Ok(new TestConnectionResponseDto(
                result.Success,
                result.Success ? "Connection successful" : result.ErrorDetails,
                result.ErrorType,
                result.LatencyMs,
                result.CheckedAt,
                result.Quota,
                result.AvailableModels?.Select(m => new ModelInfoDto(m.Id, m.Name, m.ContextSize)).ToArray()));
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
