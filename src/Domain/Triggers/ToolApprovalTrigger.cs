namespace AzureOpsCrew.Domain.Triggers;

public class ToolApprovalTrigger : ITrigger
{
    // common
    public Guid Id { get; set; }
    public TriggerType Type => TriggerType.ToolApproval;
    public Guid AgentId { get; set; }
    public Guid ChatId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    // specific
    public string CallId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;

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
            CallId = CallId,
            ToolName = ToolName,
            Parameters = Parameters,
        };
    }

    public ITrigger FromDto(Trigger dto)
    {
        if (dto.Type != TriggerType.ToolApproval)
            throw new ArgumentException($"Invalid trigger type: {dto.Type}");

        return new ToolApprovalTrigger
        {
            Id = dto.Id,
            AgentId = dto.AgentId,
            ChatId = dto.ChatId,
            CreatedAt = dto.CreatedAt,
            StartedAt = dto.StartedAt,
            CompletedAt = dto.CompletedAt,
            CallId = dto.CallId ?? string.Empty,
            ToolName = dto.ToolName ?? string.Empty,
            Parameters = dto.Parameters ?? string.Empty,
        };
    }
}
