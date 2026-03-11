namespace AzureOpsCrew.Domain.Triggers;

public record TriggerContext
{
    public Guid? MessageChatId { get; init; }
    public string? EventType { get; init; }
    public Guid? SourceAgentId { get; init; }
    public string? WebhookToken { get; init; }
    public DateTime UtcNow { get; init; } = DateTime.UtcNow;
}
