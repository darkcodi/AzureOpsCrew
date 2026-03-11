namespace AzureOpsCrew.Domain.Triggers;

public enum TriggerType
{
    Message = 0,
    Scheduled = 1,
    Webhook = 2,
    Event = 3,
}
