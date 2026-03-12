using Front.Models;

namespace Front.Services;

/// <summary>
/// Singleton service that tracks agent status in memory for the current session.
/// Status is updated via SignalR; no persistence across page refreshes.
/// </summary>
public class AgentState
{
    private readonly Reactive<Dictionary<string, AgentStatusState>> _states = new(new Dictionary<string, AgentStatusState>());
    private bool _isInitialized;

    /// <summary>
    /// Initialize the service. No-op; kept for API compatibility.
    /// </summary>
    public Task InitializeAsync()
    {
        if (!_isInitialized)
            _isInitialized = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Generate a unique key for storing/retrieving state for a (conversation, agent).
    /// </summary>
    private static string GetKey(Guid conversationId, ConversationType conversationType, Guid agentId) =>
        $"{conversationType}_{conversationId}_{agentId}";

    /// <summary>
    /// Get the state for a specific (conversation, agent).
    /// </summary>
    public AgentStatusState? GetState(Guid conversationId, ConversationType conversationType, Guid agentId)
    {
        var key = GetKey(conversationId, conversationType, agentId);
        return _states.Value.TryGetValue(key, out var state) ? state : null;
    }

    /// <summary>
    /// Get all agent states for a conversation (for "is any running" and typing).
    /// </summary>
    public IReadOnlyList<AgentStatusState> GetStatesForConversation(Guid conversationId, ConversationType conversationType)
    {
        var prefix = $"{conversationType}_{conversationId}_";
        return _states.Value
            .Where(kvp => kvp.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(kvp => kvp.Value)
            .ToList();
    }

    /// <summary>
    /// Set the agent status for a conversation (in-memory only).
    /// </summary>
    public Task SetAgentStatusAsync(
        Guid conversationId,
        ConversationType conversationType,
        Guid agentId,
        string status,
        string? errorMessage = null)
    {
        var key = GetKey(conversationId, conversationType, agentId);
        var newState = new AgentStatusState
        {
            ConversationId = conversationId,
            ConversationType = conversationType,
            AgentId = agentId,
            Status = status,
            ErrorMessage = errorMessage,
            LastUpdated = DateTimeOffset.UtcNow
        };

        var currentStates = _states.Value;
        currentStates[key] = newState;

        _states.ForceNotify();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clear all state for a specific conversation (e.g. on navigate).
    /// </summary>
    public Task ClearStateAsync(Guid conversationId, ConversationType conversationType)
    {
        var prefix = $"{conversationType}_{conversationId}_";
        var currentStates = _states.Value;
        var keysToRemove = currentStates.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        var removed = false;
        foreach (var k in keysToRemove)
        {
            if (currentStates.Remove(k))
                removed = true;
        }
        if (removed)
            _states.ForceNotify();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Check if any agent is currently running for the given conversation.
    /// </summary>
    public bool IsAgentRunning(Guid conversationId, ConversationType conversationType)
    {
        var states = GetStatesForConversation(conversationId, conversationType);
        return states.Any(s => string.Equals(s.Status, "Running", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Get the typing username for the agent in the given conversation.
    /// Returns the first running agent's username if any, otherwise null.
    /// This must be called after AppState.Agents has been loaded.
    /// </summary>
    public string? GetTypingUsername(Guid conversationId, ConversationType conversationType, Func<Guid, string?> getAgentUsername)
    {
        var states = GetStatesForConversation(conversationId, conversationType);
        var running = states.FirstOrDefault(s => string.Equals(s.Status, "Running", StringComparison.OrdinalIgnoreCase));
        return running == null ? null : getAgentUsername(running.AgentId);
    }

    /// <summary>
    /// Subscribe to changes in agent states.
    /// </summary>
    public IDisposable Subscribe(Action handler) => _states.Subscribe(handler);
}
