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

    // specific
    public Guid MessageId { get; set; }
    public Guid AuthorId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string MessageContent { get; set; } = string.Empty;

    public Trigger ToDto()
    {
        return new Trigger
        {
            Id = Id,
            Type = Type,
            AgentId = AgentId,
            ChatId = ChatId,
            CreatedAt = CreatedAt,
            StartedAt = StartedAt,
            CompletedAt = CompletedAt,
            MessageId = MessageId,
            AuthorId = AuthorId,
            AuthorName = AuthorName,
            MessageContent = MessageContent,
        };
    }

    public ITrigger FromDto(Trigger dto)
    {
        if (dto.Type != TriggerType.Message)
            throw new ArgumentException($"Invalid trigger type: {dto.Type}");

        return new MessageTrigger
        {
            Id = dto.Id,
            AgentId = dto.AgentId,
            ChatId = dto.ChatId,
            CreatedAt = dto.CreatedAt,
            StartedAt = dto.StartedAt,
            CompletedAt = dto.CompletedAt,
            MessageId = dto.MessageId ?? Guid.Empty,
            AuthorId = dto.AuthorId ?? Guid.Empty,
            AuthorName = dto.AuthorName ?? string.Empty,
            MessageContent = dto.MessageContent ?? string.Empty,
        };
    }
}
