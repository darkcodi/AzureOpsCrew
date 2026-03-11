using System.Text.Json;
using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Api.Background.Triggers;

public class WebhookTriggerHandler : ITriggerHandler
{
    public TriggerType HandledType => TriggerType.Webhook;

    public Task<bool> ShouldFireAsync(AgentTrigger trigger, TriggerContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.WebhookToken) || string.IsNullOrWhiteSpace(trigger.ConfigurationJson))
            return Task.FromResult(false);

        var config = JsonSerializer.Deserialize<WebhookTriggerConfig>(trigger.ConfigurationJson);
        if (config is null)
            return Task.FromResult(false);

        var shouldFire = string.Equals(context.WebhookToken, config.WebhookToken, StringComparison.Ordinal);
        return Task.FromResult(shouldFire);
    }
}
