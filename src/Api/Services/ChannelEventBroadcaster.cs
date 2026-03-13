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
    Task BroadcastChannelToolCallStartAsync(Guid channelId, ToolCallStartEvent evt);
    Task BroadcastChannelToolCallCompletedAsync(Guid channelId, ToolCallCompletedEvent evt);
    Task BroadcastChannelReasoningContentAsync(Guid channelId, ReasoningContentEvent evt);
    Task BroadcastChannelAgentStatusAsync(Guid channelId, AgentStatusEvent evt);
    Task BroadcastDmToolCallStartAsync(Guid dmId, ToolCallStartEvent evt);
    Task BroadcastDmToolCallCompletedAsync(Guid dmId, ToolCallCompletedEvent evt);
    Task BroadcastDmReasoningContentAsync(Guid dmId, ReasoningContentEvent evt);
    Task BroadcastDmAgentStatusAsync(Guid dmId, AgentStatusEvent evt);
    Task BroadcastChannelApprovalRequestAsync(Guid channelId, ApprovalRequestEvent evt);
    Task BroadcastDmApprovalRequestAsync(Guid dmId, ApprovalRequestEvent evt);
}

/// <summary>
/// Singleton service that provides thread-safe broadcasting of channel and DM events via SignalR.
/// </summary>
public class ChannelEventBroadcaster : IChannelEventBroadcaster
{
    private readonly IHubContext<ChannelEventsHub> _channelHubContext;
    private readonly IHubContext<DmEventsHub> _dmHubContext;
    private readonly IAgentStatusTracker _agentStatusTracker;
    private readonly ILogger<ChannelEventBroadcaster> _logger;

    public ChannelEventBroadcaster(
        IHubContext<ChannelEventsHub> channelHubContext,
        IHubContext<DmEventsHub> dmHubContext,
        IAgentStatusTracker agentStatusTracker,
        ILogger<ChannelEventBroadcaster> logger)
    {
        _channelHubContext = channelHubContext;
        _dmHubContext = dmHubContext;
        _agentStatusTracker = agentStatusTracker;
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

    public Task BroadcastChannelToolCallStartAsync(Guid channelId, ToolCallStartEvent evt)
    {
        var groupName = GetChannelGroupName(channelId);
        _logger.LogDebug("Broadcasting TOOL_CALL_START to channel {ChannelId}", channelId);
        return _channelHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    public Task BroadcastChannelToolCallCompletedAsync(Guid channelId, ToolCallCompletedEvent evt)
    {
        var groupName = GetChannelGroupName(channelId);
        _logger.LogDebug("Broadcasting TOOL_CALL_COMPLETED to channel {ChannelId}", channelId);
        return _channelHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    public Task BroadcastChannelReasoningContentAsync(Guid channelId, ReasoningContentEvent evt)
    {
        var groupName = GetChannelGroupName(channelId);
        _logger.LogDebug("Broadcasting REASONING_CONTENT to channel {ChannelId}", channelId);
        return _channelHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    public Task BroadcastChannelAgentStatusAsync(Guid channelId, AgentStatusEvent evt)
    {
        _agentStatusTracker.SetChannelStatus(channelId, evt.AgentId, evt.Status, evt.ErrorMessage);
        var groupName = GetChannelGroupName(channelId);
        _logger.LogDebug("Broadcasting AGENT_STATUS ({Status}) to channel {ChannelId}", evt.Status, channelId);
        return _channelHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    public Task BroadcastDmToolCallStartAsync(Guid dmId, ToolCallStartEvent evt)
    {
        var groupName = GetDmGroupName(dmId);
        _logger.LogDebug("Broadcasting TOOL_CALL_START to DM {DmId}", dmId);
        return _dmHubContext.Clients.Group(groupName).SendAsync("event", evt);
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

    public Task BroadcastDmAgentStatusAsync(Guid dmId, AgentStatusEvent evt)
    {
        _agentStatusTracker.SetDmStatus(dmId, evt.AgentId, evt.Status, evt.ErrorMessage);
        var groupName = GetDmGroupName(dmId);
        _logger.LogDebug("Broadcasting AGENT_STATUS ({Status}) to DM {DmId}", evt.Status, dmId);
        return _dmHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    public Task BroadcastChannelApprovalRequestAsync(Guid channelId, ApprovalRequestEvent evt)
    {
        var groupName = GetChannelGroupName(channelId);
        _logger.LogDebug("Broadcasting APPROVAL_REQUEST to channel {ChannelId}", channelId);
        return _channelHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    public Task BroadcastDmApprovalRequestAsync(Guid dmId, ApprovalRequestEvent evt)
    {
        var groupName = GetDmGroupName(dmId);
        _logger.LogDebug("Broadcasting APPROVAL_REQUEST to DM {DmId}", dmId);
        return _dmHubContext.Clients.Group(groupName).SendAsync("event", evt);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string GetChannelGroupName(Guid channelId) => $"channel_{channelId}";
    private static string GetDmGroupName(Guid dmId) => $"dm_{dmId}";
}
