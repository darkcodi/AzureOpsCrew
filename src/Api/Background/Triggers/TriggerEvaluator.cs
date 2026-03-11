using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Text.Json;

namespace AzureOpsCrew.Api.Background.Triggers;

public class TriggerEvaluator
{
    private readonly AzureOpsCrewContext _dbContext;
    private readonly AgentTriggerQueue _agentTriggerQueue;
    private readonly Dictionary<TriggerType, ITriggerHandler> _handlers;

    public TriggerEvaluator(
        AzureOpsCrewContext dbContext,
        AgentTriggerQueue agentTriggerQueue,
        IEnumerable<ITriggerHandler> handlers)
    {
        _dbContext = dbContext;
        _agentTriggerQueue = agentTriggerQueue;
        _handlers = handlers.ToDictionary(h => h.HandledType);
    }

    public async Task EvaluateAsync(TriggerContext context, TriggerType[] triggerTypes, CancellationToken ct)
    {
        var triggers = await _dbContext.Triggers
            .Where(t => t.IsEnabled)
            .ToListAsync(ct);

        triggers = triggers.Where(t => triggerTypes.Contains(t.TriggerType)).ToList();

        foreach (var trigger in triggers)
        {
            if (!_handlers.TryGetValue(trigger.TriggerType, out var handler))
                continue;

            var shouldFire = await handler.ShouldFireAsync(trigger, context, ct);
            if (!shouldFire)
                continue;

            Log.Information("[TRIGGERS] Firing trigger {TriggerId} ({TriggerType}) for agent {AgentId} in chat {ChatId}",
                trigger.Id, trigger.TriggerType, trigger.AgentId, trigger.ChatId);

            // Create execution record
            var contextJson = JsonSerializer.Serialize(context);
            var execution = new AgentTriggerExecution(trigger.Id, contextJson);
            _dbContext.TriggerExecutions.Add(execution);

            try
            {
                _agentTriggerQueue.Enqueue(trigger.AgentId, trigger.ChatId);
                trigger.MarkFired();
                execution.MarkCompleted();
            }
            catch (Exception ex)
            {
                execution.MarkFailed(ex.Message);
                Log.Error(ex, "[TRIGGERS] Failed to enqueue agent run for trigger {TriggerId}", trigger.Id);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
