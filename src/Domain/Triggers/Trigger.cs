namespace AzureOpsCrew.Domain.Triggers;

public abstract class Trigger
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsSkipped { get; set; }
}
