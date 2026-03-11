using AzureOpsCrew.Domain.Triggers;

namespace AzureOpsCrew.Domain.WaitConditions;

public abstract class WaitCondition
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? SatisfiedByTriggerId { get; set; }

    public abstract bool CanBeSatisfiedByTrigger(Trigger trigger);
}
