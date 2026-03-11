using System.Text.Json;
using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Api.Background.Triggers;

public class EventTriggerHandler : ITriggerHandler
{
    public TriggerType HandledType => TriggerType.Event;

    public Task<bool> ShouldFireAsync(AgentTrigger trigger, TriggerContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.EventType) || string.IsNullOrWhiteSpace(trigger.ConfigurationJson))
            return Task.FromResult(false);

        var config = JsonSerializer.Deserialize<EventTriggerConfig>(trigger.ConfigurationJson);
        if (config is null)
            return Task.FromResult(false);

        if (!string.Equals(context.EventType, config.EventType, StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(false);

        if (config.SourceAgentId.HasValue && context.SourceAgentId != config.SourceAgentId)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
