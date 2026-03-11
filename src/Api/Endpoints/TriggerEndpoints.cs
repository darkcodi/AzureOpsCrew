using System.Text.Json;
using AzureOpsCrew.Api.Background.Triggers;
using AzureOpsCrew.Api.Endpoints.Dtos.Triggers;
using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Endpoints;

public static class TriggerEndpoints
{
    public static void MapTriggerEndpoints(this IEndpointRouteBuilder routeBuilder)
    {
        var group = routeBuilder.MapGroup("/api/triggers")
            .WithTags("Triggers")
            .RequireAuthorization();

        group.MapPost("/create", async (
            CreateTriggerDto dto,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            if (!Enum.TryParse<TriggerType>(dto.TriggerType, ignoreCase: true, out var triggerType))
                return Results.BadRequest($"Invalid trigger type: {dto.TriggerType}");

            var configJson = dto.ConfigurationJson;

            // Auto-generate WebhookToken for webhook triggers
            if (triggerType == TriggerType.Webhook)
            {
                var token = Guid.NewGuid().ToString("N");
                var webhookConfig = new WebhookTriggerConfig(token, null);
                configJson = JsonSerializer.Serialize(webhookConfig);
            }

            var trigger = new AgentTrigger(
                Guid.NewGuid(),
                dto.AgentId,
                dto.ChatId,
                triggerType,
                configJson);

            await context.Triggers.AddAsync(trigger, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Created($"/api/triggers/{trigger.Id}", trigger.ToResponseDto());
        })
        .Produces<TriggerResponseDto>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        group.MapGet("", async (
            Guid? agentId,
            Guid? chatId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var query = context.Triggers.AsQueryable();

            if (agentId.HasValue)
                query = query.Where(t => t.AgentId == agentId.Value);

            if (chatId.HasValue)
                query = query.Where(t => t.ChatId == chatId.Value);

            var triggers = await query
                .OrderBy(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            return Results.Ok(triggers.Select(t => t.ToResponseDto()));
        })
        .Produces<TriggerResponseDto[]>(StatusCodes.Status200OK);

        group.MapGet("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var trigger = await context.Triggers
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

            return trigger is null
                ? Results.NotFound()
                : Results.Ok(trigger.ToResponseDto());
        })
        .Produces<TriggerResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPut("/{id}", async (
            Guid id,
            UpdateTriggerDto dto,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var trigger = await context.Triggers
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (trigger is null)
                return Results.NotFound();

            trigger.UpdateConfiguration(dto.ConfigurationJson);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(trigger.ToResponseDto());
        })
        .Produces<TriggerResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/set-enabled", async (
            Guid id,
            SetTriggerEnabledDto dto,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var trigger = await context.Triggers
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (trigger is null)
                return Results.NotFound();

            trigger.SetEnabled(dto.IsEnabled);
            await context.SaveChangesAsync(cancellationToken);

            return Results.Ok(trigger.ToResponseDto());
        })
        .Produces<TriggerResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        group.MapDelete("/{id}", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var trigger = await context.Triggers
                .SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

            if (trigger is null)
                return Results.NotFound();

            context.Triggers.Remove(trigger);
            await context.SaveChangesAsync(cancellationToken);

            return Results.NoContent();
        })
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{id}/executions", async (
            Guid id,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken,
            int limit = 50) =>
        {
            var executions = await context.TriggerExecutions
                .Where(e => e.TriggerId == id)
                .OrderByDescending(e => e.FiredAt)
                .Take(limit)
                .Select(e => new
                {
                    e.Id,
                    e.FiredAt,
                    e.Success,
                    e.ErrorMessage,
                    e.CompletedAt
                })
                .ToListAsync(cancellationToken);

            return Results.Ok(executions);
        })
        .WithTags("Triggers")
        .Produces(StatusCodes.Status200OK);

        // Webhook ingress - anonymous, outside auth group
        routeBuilder.MapPost("/api/triggers/webhook/{token}", async (
            string token,
            TriggerEvaluator evaluator,
            CancellationToken cancellationToken) =>
        {
            var triggerContext = new TriggerContext { WebhookToken = token };
            await evaluator.EvaluateAsync(triggerContext, [TriggerType.Webhook], cancellationToken);

            return Results.Ok(new { received = true });
        })
        .WithTags("Triggers")
        .Produces(StatusCodes.Status200OK);
    }
}
