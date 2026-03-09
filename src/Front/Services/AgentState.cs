using System.Text.Json;
using Front.Models;
using Microsoft.JSInterop;
using Serilog;

namespace Front.Services;

/// <summary>
/// Singleton service that tracks and persists agent status across page refreshes.
/// Stores agent status (running/idle/error) in localStorage for each conversation.
/// </summary>
public class AgentState
{
    private const string StorageKey = "agent_status_states";
    private readonly Reactive<Dictionary<string, AgentStatusState>> _states = new(new Dictionary<string, AgentStatusState>());
    private readonly IJSRuntime _js;
    private bool _isInitialized;

    public AgentState(IJSRuntime js)
    {
        _js = js;
    }

    /// <summary>
    /// Initialize the service by loading states from localStorage.
    /// Filters out states older than 1 hour to prevent stale data.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;

        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(raw))
            {
                var loadedStates = JsonSerializer.Deserialize<Dictionary<string, AgentStatusState>>(raw, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });

                if (loadedStates != null)
                {
                    // Filter out states older than 1 hour
                    var cutoff = DateTimeOffset.UtcNow.AddHours(-1);
                    var validStates = loadedStates
                        .Where(kvp => kvp.Value.LastUpdated > cutoff)
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                    _states.Value = validStates;

                    var removedCount = loadedStates.Count - validStates.Count;
                    if (removedCount > 0)
                    {
                        Log.Information("Filtered out {Count} stale agent states (older than 1 hour)", removedCount);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load agent states from localStorage");
        }

        _isInitialized = true;
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
    /// Set the agent status for a conversation and persist to localStorage.
    /// </summary>
    public async Task SetAgentStatusAsync(
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

        // Trigger change notification
        _states.ForceNotify();

        // Persist to localStorage
        await PersistToLocalStorageAsync();
    }

    /// <summary>
    /// Clear the state for a specific conversation.
    /// </summary>
    public async Task ClearStateAsync(Guid conversationId, ConversationType conversationType)
    {
        var key = GetKey(conversationId, conversationType);
        var currentStates = _states.Value;
        if (currentStates.Remove(key))
        {
            // Trigger change notification
            _states.ForceNotify();
            await PersistToLocalStorageAsync();
        }
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

    /// <summary>
    /// Persist current states to localStorage.
    /// </summary>
    private async Task PersistToLocalStorageAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_states.Value, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to persist agent states to localStorage");
        }
    }
}
