using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
            .Where(t => t.IsEnabled && triggerTypes.Contains(t.TriggerType))
            .ToListAsync(ct);

        foreach (var trigger in triggers)
        {
            if (!_handlers.TryGetValue(trigger.TriggerType, out var handler))
                continue;

            var shouldFire = await handler.ShouldFireAsync(trigger, context, ct);
            if (!shouldFire)
                continue;

            Log.Information("[TRIGGERS] Firing trigger {TriggerId} ({TriggerType}) for agent {AgentId} in chat {ChatId}",
                trigger.Id, trigger.TriggerType, trigger.AgentId, trigger.ChatId);

            _agentTriggerQueue.Enqueue(trigger.AgentId, trigger.ChatId);
            trigger.MarkFired();
        }

        await _dbContext.SaveChangesAsync(ct);
    }
}
