namespace AzureOpsCrew.Domain.WaitConditions;

public class WaitCondition
{
    // common
    public Guid Id { get; set; }
    public WaitConditionType Type { get; set; }
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? SatisfiedByTriggerId { get; set; }

    // specific to wait message trigger
    public Guid? AfterMessageId { get; set; }

    // specific to wait tool approval trigger
    public string? ToolCallId { get; set; }
}
