using AzureOpsCrew.Domain.Tools;

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
    public bool IsSkipped { get; set; }

    // message trigger specific
    public Guid? MessageId { get; set; }
    public Guid? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? MessageContent { get; set; }

    // tool approval trigger specific
    public string? CallId { get; set; }
    public ApprovalResolution? Resolution { get; set; }
    public string? ToolName { get; set; }
    public string? Parameters { get; set; }

    public ITrigger ToSpecificTrigger()
    {
        return Type switch
        {
            TriggerType.Message => new MessageTrigger
            {
                Id = Id,
                AgentId = AgentId,
                ChatId = ChatId,
                CreatedAt = CreatedAt,
                StartedAt = StartedAt,
                CompletedAt = CompletedAt,
                IsSkipped = IsSkipped,
                MessageId = MessageId ?? Guid.Empty,
                AuthorId = AuthorId ?? Guid.Empty,
                AuthorName = AuthorName ?? string.Empty,
                MessageContent = MessageContent ?? string.Empty,
            },
            TriggerType.ToolApproval => new ToolApprovalTrigger
            {
                Id = Id,
                AgentId = AgentId,
                ChatId = ChatId,
                CreatedAt = CreatedAt,
                StartedAt = StartedAt,
                CompletedAt = CompletedAt,
                IsSkipped = IsSkipped,
                CallId = CallId ?? string.Empty,
                Resolution = Resolution ?? ApprovalResolution.None,
                ToolName = ToolName ?? string.Empty,
                Parameters = Parameters ?? string.Empty,
            },
        };
    }

    public static Trigger FromSpecificTrigger(ITrigger trigger)
    {
        return trigger.Type switch
        {
            TriggerType.Message => new Trigger
            {
                Id = trigger.Id,
                Type = trigger.Type,
                AgentId = trigger.AgentId,
                ChatId = trigger.ChatId,
                CreatedAt = trigger.CreatedAt,
                StartedAt = trigger.StartedAt,
                CompletedAt = trigger.CompletedAt,
                IsSkipped = ((MessageTrigger)trigger).IsSkipped,
                MessageId = ((MessageTrigger)trigger).MessageId,
                AuthorId = ((MessageTrigger)trigger).AuthorId,
                AuthorName = ((MessageTrigger)trigger).AuthorName,
                MessageContent = ((MessageTrigger)trigger).MessageContent,
            },
            TriggerType.ToolApproval => new Trigger
            {
                Id = trigger.Id,
                Type = trigger.Type,
                AgentId = trigger.AgentId,
                ChatId = trigger.ChatId,
                CreatedAt = trigger.CreatedAt,
                StartedAt = trigger.StartedAt,
                CompletedAt = trigger.CompletedAt,
                IsSkipped = ((ToolApprovalTrigger)trigger).IsSkipped,
                CallId = ((ToolApprovalTrigger)trigger).CallId,
                Resolution = ((ToolApprovalTrigger)trigger).Resolution,
                ToolName = ((ToolApprovalTrigger)trigger).ToolName,
                Parameters = ((ToolApprovalTrigger)trigger).Parameters,
            },
        };
    }
}
