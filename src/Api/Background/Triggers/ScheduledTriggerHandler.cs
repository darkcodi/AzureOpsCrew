using System.Text.Json;
using AzureOpsCrew.Domain.Triggers;
using Cronos;

namespace AzureOpsCrew.Api.Background.Triggers;

public class ScheduledTriggerHandler : ITriggerHandler
{
    public TriggerType HandledType => TriggerType.Scheduled;

    public Task<bool> ShouldFireAsync(AgentTrigger trigger, TriggerContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(trigger.ConfigurationJson))
            return Task.FromResult(false);

        var config = JsonSerializer.Deserialize<ScheduledTriggerConfig>(trigger.ConfigurationJson);
        if (config is null || string.IsNullOrWhiteSpace(config.CronExpression))
            return Task.FromResult(false);

        var expression = CronExpression.Parse(config.CronExpression);
        var baseTime = trigger.LastFiredAt ?? trigger.CreatedAt;
        var nextOccurrence = expression.GetNextOccurrence(baseTime, inclusive: false);

        var shouldFire = nextOccurrence.HasValue && nextOccurrence.Value <= context.UtcNow;
        return Task.FromResult(shouldFire);
    }
}
