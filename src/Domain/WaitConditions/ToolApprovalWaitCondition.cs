namespace AzureOpsCrew.Domain.WaitConditions;

public class ToolApprovalWaitCondition : IWaitCondition
{
    // common
    public Guid Id { get; set; }
    public WaitConditionType Type => WaitConditionType.ToolApproval;
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? SatisfiedByTriggerId { get; set; }

    // specific to wait tool approval trigger
    public string ToolCallId { get; set; } = string.Empty;

    public WaitCondition ToDto()
    {
        return new WaitCondition
        {
            Id = Id,
            Type = Type,
            AgentId = AgentId,
            ChatId = ChatId,
            CreatedAt = CreatedAt,
            CompletedAt = CompletedAt,
            SatisfiedByTriggerId = SatisfiedByTriggerId,
            ToolCallId = ToolCallId,
        };
    }

    public IWaitCondition FromDto(WaitCondition dto)
    {
        if (dto.Type != WaitConditionType.ToolApproval)
            throw new ArgumentException($"Invalid wait condition type: {dto.Type}");

        return new ToolApprovalWaitCondition
        {
            Id = dto.Id,
            AgentId = dto.AgentId,
            ChatId = dto.ChatId,
            CreatedAt = dto.CreatedAt,
            CompletedAt = dto.CompletedAt,
            SatisfiedByTriggerId = dto.SatisfiedByTriggerId,
            ToolCallId = dto.ToolCallId ?? string.Empty,
        };
    }
}
