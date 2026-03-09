namespace AzureOpsCrew.Domain.Orchestration;

public class OrchestrationTask
{
    public Guid Id { get; set; }
    public Guid ChannelId { get; set; }
    public Guid CreatedByAgentId { get; set; }
    public Guid AssignedAgentId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public OrchestrationTaskStatus Status { get; set; }
    public string? ProgressSummary { get; set; }
    public string? ResultSummary { get; set; }
    public string? FailureReason { get; set; }
    public bool AnnounceInChat { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? FailedAtUtc { get; set; }
}
