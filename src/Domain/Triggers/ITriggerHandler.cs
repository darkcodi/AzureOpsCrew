namespace AzureOpsCrew.Domain.Triggers;

public interface ITriggerHandler
{
    TriggerType HandledType { get; }
    Task<bool> ShouldFireAsync(AgentTrigger trigger, TriggerContext context, CancellationToken ct);
}
