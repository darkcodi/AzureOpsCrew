using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Api.Background.Triggers;

public class MessageTriggerHandler : ITriggerHandler
{
    public TriggerType HandledType => TriggerType.Message;

    public Task<bool> ShouldFireAsync(AgentTrigger trigger, TriggerContext context, CancellationToken ct)
    {
        var shouldFire = context.MessageChatId == trigger.ChatId;
        return Task.FromResult(shouldFire);
    }
}
