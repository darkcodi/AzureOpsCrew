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
    /// Generate a unique key for storing/retrieving state for a conversation.
    /// </summary>
    private static string GetKey(Guid conversationId, ConversationType conversationType) =>
        $"{conversationType}_{conversationId}";

    /// <summary>
    /// Get the state for a specific conversation.
    /// </summary>
    public AgentStatusState? GetState(Guid conversationId, ConversationType conversationType)
    {
        var key = GetKey(conversationId, conversationType);
        return _states.Value.TryGetValue(key, out var state) ? state : null;
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
        var key = GetKey(conversationId, conversationType);
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
    /// Clear the state for a specific conversation.
    /// </summary>
    public Task ClearStateAsync(Guid conversationId, ConversationType conversationType)
    {
        var key = GetKey(conversationId, conversationType);
        var currentStates = _states.Value;
        if (currentStates.Remove(key))
            _states.ForceNotify();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Check if an agent is currently running for the given conversation.
    /// </summary>
    public bool IsAgentRunning(Guid conversationId, ConversationType conversationType)
    {
        var state = GetState(conversationId, conversationType);
        return state != null && string.Equals(state.Status, "Running", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Get the typing username for the agent in the given conversation.
    /// Returns the agent's username if the agent is running, otherwise null.
    /// This must be called after AppState.Agents has been loaded.
    /// </summary>
    public string? GetTypingUsername(Guid conversationId, ConversationType conversationType, Func<Guid, string?> getAgentUsername)
    {
        var state = GetState(conversationId, conversationType);
        if (state == null || !string.Equals(state.Status, "Running", StringComparison.OrdinalIgnoreCase))
            return null;

        return getAgentUsername(state.AgentId);
    }

    /// <summary>
    /// Subscribe to changes in agent states.
    /// </summary>
    public IDisposable Subscribe(Action handler) => _states.Subscribe(handler);
}
