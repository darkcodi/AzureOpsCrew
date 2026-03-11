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
    public DateTime? MessageAfterDateTime { get; set; }

    // specific to wait tool approval trigger
    public string? ToolCallId { get; set; }

    public IWaitCondition ToSpecificWaitCondition()
    {
        return Type switch
        {
            WaitConditionType.Message => new MessageWaitCondition
            {
                Id = Id,
                AgentId = AgentId,
                ChatId = ChatId,
                CreatedAt = CreatedAt,
                CompletedAt = CompletedAt,
                SatisfiedByTriggerId = SatisfiedByTriggerId,
                MessageAfterDateTime = MessageAfterDateTime ?? DateTime.MinValue,
            },
            WaitConditionType.ToolApproval => new ToolApprovalWaitCondition
            {
                Id = Id,
                AgentId = AgentId,
                ChatId = ChatId,
                CreatedAt = CreatedAt,
                CompletedAt = CompletedAt,
                SatisfiedByTriggerId = SatisfiedByTriggerId,
                ToolCallId = ToolCallId ?? string.Empty,
            },
        };
    }

    public static WaitCondition FromSpecificWaitCondition(IWaitCondition waitCondition)
    {
        return waitCondition.Type switch
        {
            WaitConditionType.Message => new WaitCondition
            {
                Id = waitCondition.Id,
                Type = waitCondition.Type,
                AgentId = waitCondition.AgentId,
                ChatId = waitCondition.ChatId,
                CreatedAt = waitCondition.CreatedAt,
                CompletedAt = waitCondition.CompletedAt,
                SatisfiedByTriggerId = waitCondition.SatisfiedByTriggerId,
                MessageAfterDateTime = ((MessageWaitCondition)waitCondition).MessageAfterDateTime,
            },
            WaitConditionType.ToolApproval => new WaitCondition
            {
                Id = waitCondition.Id,
                Type = waitCondition.Type,
                AgentId = waitCondition.AgentId,
                ChatId = waitCondition.ChatId,
                CreatedAt = waitCondition.CreatedAt,
                CompletedAt = waitCondition.CompletedAt,
                SatisfiedByTriggerId = waitCondition.SatisfiedByTriggerId,
                ToolCallId = ((ToolApprovalWaitCondition)waitCondition).ToolCallId,
            },
        };
    }
}
