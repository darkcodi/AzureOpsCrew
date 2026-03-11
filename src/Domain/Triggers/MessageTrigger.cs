namespace AzureOpsCrew.Domain.Triggers;

public class MessageTrigger : Trigger
{
    public Guid MessageId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;
}
