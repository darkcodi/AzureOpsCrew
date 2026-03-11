namespace AzureOpsCrew.Domain.Triggers;

public class MessageTrigger : ITrigger
{
    // common
    public Guid Id { get; set; }
    public TriggerType Type => TriggerType.Message;
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsSkipped { get; set; }

    // specific
    public Guid MessageId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
}
