namespace AzureOpsCrew.Domain.Triggers;

public class Trigger
{
    // common
    public Guid Id { get; set; }
    public TriggerType Type { get; set; }
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // message trigger specific
    public Guid? MessageId { get; set; }
    public Guid? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? MessageContent { get; set; }

    // tool approval trigger specific
    public string? CallId { get; set; }
    public string? ToolName { get; set; }
    public string? Parameters { get; set; }
}
