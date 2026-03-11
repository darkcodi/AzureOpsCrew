namespace AzureOpsCrew.Domain.WaitConditions;

public class MessageWaitCondition : IWaitCondition
{
    // common
    public Guid Id { get; set; }
    public WaitConditionType Type => WaitConditionType.Message;
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? SatisfiedByTriggerId { get; set; }

    // specific to wait message trigger
    public Guid AfterMessageId { get; set; }

    public WaitCondition ToDto()
    {
        return new WaitCondition
        {
            Id = Id,
            AgentId = AgentId,
            ChatId = ChatId,
            CreatedAt = CreatedAt,
            CompletedAt = CompletedAt,
            SatisfiedByTriggerId = SatisfiedByTriggerId,
            AfterMessageId = AfterMessageId,
        };
    }

    public IWaitCondition FromDto(WaitCondition dto)
    {
        if (dto.Type != WaitConditionType.Message)
            throw new ArgumentException($"Invalid wait condition type: {dto.Type}");

        return new MessageWaitCondition
        {
            Id = dto.Id,
            AgentId = dto.AgentId,
            ChatId = dto.ChatId,
            CreatedAt = dto.CreatedAt,
            CompletedAt = dto.CompletedAt,
            SatisfiedByTriggerId = dto.SatisfiedByTriggerId,
            AfterMessageId = dto.AfterMessageId ?? Guid.Empty,
        };
    }
}
