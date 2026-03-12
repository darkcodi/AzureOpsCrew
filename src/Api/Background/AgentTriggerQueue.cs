using AzureOpsCrew.Domain.Orchestration;
using Serilog;

namespace AzureOpsCrew.Api.Background;

public class AgentTriggerQueue
{
    private readonly object _lock = new();
    private readonly List<AgentTrigger> _pendingTriggers = new();

    /// <summary>
    /// Enqueue a rich trigger.
    /// TaskAssigned triggers are deduped by exact task identity so multiple tasks for one worker are preserved.
    /// User/manager coordination triggers are deduped by (AgentId, ChatId, Kind).
    /// </summary>
    public void Enqueue(AgentTrigger trigger)
    {
        lock (_lock)
        {
            var duplicateExists = trigger.Kind switch
            {
                AgentTriggerKind.TaskAssigned => _pendingTriggers.Any(t =>
                    t.Kind == AgentTriggerKind.TaskAssigned &&
                    t.AgentId == trigger.AgentId &&
                    t.ChatId == trigger.ChatId &&
                    t.TaskId == trigger.TaskId),
                AgentTriggerKind.UserMessage or AgentTriggerKind.TaskUpdated => _pendingTriggers.Any(t =>
                    t.Kind == trigger.Kind &&
                    t.AgentId == trigger.AgentId &&
                    t.ChatId == trigger.ChatId),
                _ => false,
            };

            if (duplicateExists)
            {
                Log.Debug(
                    "[BACKGROUND] Suppressed duplicate agent trigger: kind={TriggerKind}, agent={AgentId}, chat={ChatId}, taskId={TaskId}",
                    trigger.Kind, trigger.AgentId, trigger.ChatId, trigger.TaskId);
                return;
            }

            _pendingTriggers.Add(trigger);
            Log.Debug(
                "[BACKGROUND] Enqueued agent trigger: kind={TriggerKind}, agent={AgentId}, chat={ChatId}, taskId={TaskId}",
                trigger.Kind, trigger.AgentId, trigger.ChatId, trigger.TaskId);
        }
    }

    /// <summary>
    /// Legacy overload for backward compatibility with non-orchestrated channels.
    /// </summary>
    public void Enqueue(Guid agentId, Guid chatId)
    {
        Enqueue(AgentTrigger.UserMessage(agentId, chatId));
    }

    public List<AgentTrigger> DequeueAll()
    {
        lock (_lock)
        {
            var triggers = new List<AgentTrigger>(_pendingTriggers);
            _pendingTriggers.Clear();
            return triggers;
        }
    }
}
