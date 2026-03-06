using AzureOpsCrew.Api.Channels;
using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Domain.Chats;
using Microsoft.AspNetCore.SignalR;

namespace AzureOpsCrew.Api.Services;

/// <summary>
/// Interface for broadcasting channel and DM events via SignalR.
/// </summary>
public interface IChannelEventBroadcaster
{
    Task BroadcastMessageAddedAsync(Guid channelId, Message message);
    Task BroadcastDmMessageAddedAsync(Guid dmId, Message message);
    Task BroadcastDmToolCallCompletedAsync(Guid dmId, ToolCallCompletedEvent evt);
    Task BroadcastDmReasoningContentAsync(Guid dmId, ReasoningContentEvent evt);
}

/// <summary>
/// Singleton service that provides thread-safe broadcasting of channel and DM events via SignalR.
/// </summary>
public class ChannelEventBroadcaster : IChannelEventBroadcaster
{
    private readonly IHubContext<ChannelEventsHub> _channelHubContext;
    private readonly IHubContext<DmEventsHub> _dmHubContext;
    private readonly ILogger<ChannelEventBroadcaster> _logger;

    public ChannelEventBroadcaster(
        IHubContext<ChannelEventsHub> channelHubContext,
        IHubContext<DmEventsHub> dmHubContext,
        ILogger<ChannelEventBroadcaster> logger)
    {
        _channelHubContext = channelHubContext;
        _dmHubContext = dmHubContext;
        _logger = logger;
    }

    public Task BroadcastMessageAddedAsync(Guid channelId, Message message)
    {
        var eventMessage = new MessageAddedEvent { Message = message };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting MESSAGE_ADDED to channel {ChannelId}", channelId);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmMessageAddedAsync(Guid dmId, Message message)
    {
        var eventMessage = new MessageAddedEvent { Message = message };
        var groupName = GetDmGroupName(dmId);

        _logger.LogDebug("Broadcasting MESSAGE_ADDED to DM {DmId}", dmId);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmToolCallCompletedAsync(Guid dmId, ToolCallCompletedEvent evt)
    {
        var groupName = GetDmGroupName(dmId);
        _logger.LogDebug("Broadcasting TOOL_CALL_COMPLETED to DM {DmId}", dmId);
        return _dmHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    public Task BroadcastDmReasoningContentAsync(Guid dmId, ReasoningContentEvent evt)
    {
        var groupName = GetDmGroupName(dmId);
        _logger.LogDebug("Broadcasting REASONING_CONTENT to DM {DmId}", dmId);
        return _dmHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string GetChannelGroupName(Guid channelId) => $"channel_{channelId}";
    private static string GetDmGroupName(Guid dmId) => $"dm_{dmId}";
}
