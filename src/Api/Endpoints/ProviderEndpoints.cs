using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Endpoints.Dtos.Providers;
using AzureOpsCrew.Api.Endpoints.Filters;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class ProviderEndpoints
{
    public static void MapProviderEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/providers")
            .WithTags("Providers")
            .RequireAuthorization();

        // CREATE
        group.MapPost("/create", async (
            CreateProviderBodyDto body,
            AzureOpsCrewContext context,
            IProviderFacadeResolver providerServiceFactory,
            CancellationToken cancellationToken) =>
        {
            var config = new Provider(
                Guid.NewGuid(),
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
                config.SetModelsCount(testResult.AvailableModels.Length);

            await context.AddAsync(config, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/providers/{config.Id}", config.ToResponseDto());
        })
        .AddEndpointFilter<ValidationFilter<CreateProviderBodyDto>>()
        .Produces<ProviderResponseDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        // LIST (current user)
        group.MapGet("", async (
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var configs = await context.Set<Provider>()
                .OrderBy(p => p.DateCreated)
                .ToListAsync(cancellationToken);

            return Results.Ok(configs.ToResponseDtoArray());
        })
        .Produces<ProviderResponseDto[]>(StatusCodes.Status200OK);

        // GET by ID
        group.MapGet("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.Set<Provider>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            return found is null ? Results.NotFound() : Results.Ok(found.ToResponseDto());
        })
        .Produces<ProviderResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // UPDATE
        group.MapPut("/{id}", async (
            Guid id,
            UpdateProviderBodyDto body,
            AzureOpsCrewContext context,
            IProviderFacadeResolver providerServiceFactory,
            CancellationToken cancellationToken) =>
        {
            var found = await context.Set<Provider>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            // Only update API key if a non-empty value is provided
            var updateApiKey = !string.IsNullOrEmpty(body.ApiKey);
            found.Update(
                body.Name,
                updateApiKey ? body.ApiKey : found.ApiKey,
                body.ApiEndpoint,
                body.DefaultModel,
                body.IsEnabled,
                body.SelectedModels);

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
                found.SetModelsCount(testResult.AvailableModels.Length);

            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(found.ToResponseDto());
        })
        .AddEndpointFilter<ValidationFilter<UpdateProviderBodyDto>>()
        .Produces<ProviderResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);

        // DELETE
        group.MapDelete("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var found = await context.Set<Provider>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (found is null)
                return Results.NotFound();

            var assignedAgents = await context.Agents
                .AsNoTracking()
                .Where(a => a.ProviderId == id)
                .Select(a => a.Info.Username)
                .ToListAsync(cancellationToken);

            if (assignedAgents.Count > 0)
            {
                return Results.Conflict(new
                {
                    Error = "Provider is assigned to agents and cannot be deleted.",
                    AssignedAgents = assignedAgents
                });
            }

            context.Set<Provider>().Remove(found);
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status409Conflict)
        .Produces(StatusCodes.Status404NotFound);

        // TEST CONNECTION (by inline config, for drafts / not-yet-saved providers) — must be before /{id}/test
        group.MapPost("/test", async (
            TestConnectionBodyDto body,
            IProviderFacadeResolver providerServiceFactory,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            Provider config;

            // If providerId is supplied, fetch from database
            if (body.ProviderId.HasValue)
            {
                var existing = await context.Set<Provider>()
                    .SingleOrDefaultAsync(p => p.Id == body.ProviderId.Value, cancellationToken);

                if (existing is null)
                    return Results.NotFound("Provider not found");

                // Create test config with stored key (override with body values if provided)
                var testKey = string.IsNullOrEmpty(body.ApiKey) ? existing.ApiKey : body.ApiKey;
                var testEndpoint = string.IsNullOrEmpty(body.ApiEndpoint) ? existing.ApiEndpoint : body.ApiEndpoint;
                var testDefaultModel = string.IsNullOrEmpty(body.DefaultModel) ? existing.DefaultModel : body.DefaultModel;

                config = new Provider(
                    existing.Id,
                    body.Name ?? existing.Name,
                    existing.ProviderType,
                    testKey,
                    testEndpoint,
                    testDefaultModel,
                    true);
            }
            else
            {
                config = new Provider(
                    Guid.Empty,
                    body.Name ?? "Test",
                    body.ProviderType,
                    body.ApiKey,
                    body.ApiEndpoint,
                    body.DefaultModel,
                    true);
            }

            var service = providerServiceFactory.GetService(config.ProviderType);
            var result = await service.TestConnectionAsync(config, cancellationToken);

            return Results.Ok(new TestConnectionResponseDto(
                result.Success,
                result.Success ? "Connection successful" : result.ErrorDetails,
                result.ErrorType,
                result.LatencyMs,
                result.CheckedAt,
                result.Quota,
                result.AvailableModels?.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(m => new ModelInfoDto(m.Id, m.Name, m.ContextSize)).ToArray()));
        })
        .AddEndpointFilter<ValidationFilter<TestConnectionBodyDto>>()
        .Produces<TestConnectionResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        // TEST CONNECTION (by saved provider id)
        group.MapPost("/{id}/test", async (
            Guid id,
            IProviderFacadeResolver providerServiceFactory,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var config = await context.Set<Provider>()
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
                result.AvailableModels?.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(m => new ModelInfoDto(m.Id, m.Name, m.ContextSize)).ToArray()));
        })
        .Produces<TestConnectionResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        // LIST MODELS
        group.MapGet("/{id}/models", async (
            Guid id,
            IProviderFacadeResolver providerServiceFactory,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var config = await context.Set<Provider>()
                .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

            if (config is null)
                return Results.NotFound();

            var service = providerServiceFactory.GetService(config.ProviderType);
            var models = await service.ListModelsAsync(config, cancellationToken);

            return Results.Ok(models.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase).ToArray());
        })
        .Produces<ProviderModelInfo[]>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
