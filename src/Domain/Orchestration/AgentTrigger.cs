namespace AzureOpsCrew.Domain.Orchestration;

public sealed class AgentTrigger
{
    public Guid AgentId { get; init; }
    public Guid ChatId { get; init; }
    public AgentTriggerKind Kind { get; init; }
    public Guid? TaskId { get; init; }

    /// <summary>
    /// Creates a legacy-compatible trigger for non-orchestrated channels.
    /// </summary>
    public static AgentTrigger UserMessage(Guid agentId, Guid chatId)
        => new() { AgentId = agentId, ChatId = chatId, Kind = AgentTriggerKind.UserMessage };

    public static AgentTrigger TaskAssigned(Guid agentId, Guid chatId, Guid taskId)
        => new() { AgentId = agentId, ChatId = chatId, Kind = AgentTriggerKind.TaskAssigned, TaskId = taskId };

    public static AgentTrigger TaskUpdated(Guid agentId, Guid chatId, Guid taskId)
        => new() { AgentId = agentId, ChatId = chatId, Kind = AgentTriggerKind.TaskUpdated, TaskId = taskId };
}
