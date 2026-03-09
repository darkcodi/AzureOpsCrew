using AzureOpsCrew.Domain.Orchestration;

namespace AzureOpsCrew.Api.Background;

public class AgentTriggerQueue
{
    private readonly object _lock = new();
    private readonly List<AgentTrigger> _pendingTriggers = new();

    /// <summary>
    /// Enqueue a rich trigger. Deduplicates by (AgentId, ChatId) — newer trigger replaces older one.
    /// </summary>
    public void Enqueue(AgentTrigger trigger)
    {
        lock (_lock)
        {
            // Remove any existing trigger for the same agent+chat (replace semantics)
            _pendingTriggers.RemoveAll(t => t.AgentId == trigger.AgentId && t.ChatId == trigger.ChatId);
            _pendingTriggers.Add(trigger);
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
