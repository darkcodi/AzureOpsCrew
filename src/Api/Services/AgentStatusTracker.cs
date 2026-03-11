using System.Collections.Concurrent;

namespace AzureOpsCrew.Api.Services;

/// <summary>
/// Conversation type for agent status (channel or DM).
/// </summary>
public enum AgentStatusConversationType
{
    Channel,
    Dm
}

/// <summary>
/// Current agent status for a single conversation (channel or DM).
/// </summary>
public record AgentStatusEntry
{
    public required AgentStatusConversationType ConversationType { get; init; }
    public required Guid ConversationId { get; init; }
    public required Guid AgentId { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
}

/// <summary>
/// Tracks broadcasted agent status in memory (one status per channel/DM per agent). Thread-safe.
/// </summary>
public interface IAgentStatusTracker
{
    void SetChannelStatus(Guid channelId, Guid agentId, string status, string? errorMessage = null);
    void SetDmStatus(Guid dmId, Guid agentId, string status, string? errorMessage = null);
    AgentStatusEntry? GetChannelStatus(Guid channelId, Guid agentId);
    AgentStatusEntry? GetDmStatus(Guid dmId, Guid agentId);
}

/// <summary>
/// Singleton in-memory store of agent status per (conversation, agent). Updated whenever status is broadcast via SignalR.
/// </summary>
public class AgentStatusTracker : IAgentStatusTracker
{
    private static string ChannelKey(Guid channelId, Guid agentId) => $"Channel_{channelId}_{agentId}";
    private static string DmKey(Guid dmId, Guid agentId) => $"Dm_{dmId}_{agentId}";

    private readonly ConcurrentDictionary<string, AgentStatusEntry> _store = new();

    public void SetChannelStatus(Guid channelId, Guid agentId, string status, string? errorMessage = null)
    {
        var entry = new AgentStatusEntry
        {
            ConversationType = AgentStatusConversationType.Channel,
            ConversationId = channelId,
            AgentId = agentId,
            Status = status,
            ErrorMessage = errorMessage,
            LastUpdated = DateTimeOffset.UtcNow
        };
        _store[ChannelKey(channelId, agentId)] = entry;
    }

    public void SetDmStatus(Guid dmId, Guid agentId, string status, string? errorMessage = null)
    {
        var entry = new AgentStatusEntry
        {
            ConversationType = AgentStatusConversationType.Dm,
            ConversationId = dmId,
            AgentId = agentId,
            Status = status,
            ErrorMessage = errorMessage,
            LastUpdated = DateTimeOffset.UtcNow
        };
        _store[DmKey(dmId, agentId)] = entry;
    }

    public AgentStatusEntry? GetChannelStatus(Guid channelId, Guid agentId) =>
        _store.TryGetValue(ChannelKey(channelId, agentId), out var entry) ? entry : null;

    public AgentStatusEntry? GetDmStatus(Guid dmId, Guid agentId) =>
        _store.TryGetValue(DmKey(dmId, agentId), out var entry) ? entry : null;
}
