namespace AzureOpsCrew.Api.Background;

public class AgentTriggerQueue
{
    private readonly object _lock = new();
    private readonly List<(Guid, Guid)> _pendingTriggers = new();

    public void Enqueue(Guid agentId, Guid chatId)
    {
        lock (_lock)
        {
            if (!_pendingTriggers.Contains((agentId, chatId)))
            {
                _pendingTriggers.Add((agentId, chatId));
            }
        }
    }

    public List<(Guid agentId, Guid chatId)> DequeueAll()
    {
        lock (_lock)
        {
            var triggers = new List<(Guid, Guid)>(_pendingTriggers);
            _pendingTriggers.Clear();
            return triggers;
        }
    }
}
