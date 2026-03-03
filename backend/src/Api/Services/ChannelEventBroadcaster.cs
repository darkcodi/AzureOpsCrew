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
    // Channel events
    Task BroadcastMessageAddedAsync(Guid channelId, Message message);
    Task BroadcastAgentThinkingStartAsync(Guid channelId, Guid agentId, string agentName);
    Task BroadcastAgentThinkingEndAsync(Guid channelId, Guid agentId, string agentName);
    Task BroadcastAgentTextContentAsync(Guid channelId, Guid agentId, string agentName, string content, bool isDelta = true);
    Task BroadcastToolCallStartAsync(Guid channelId, Guid agentId, string agentName, string toolName, string toolCallId);
    Task BroadcastToolCallEndAsync(Guid channelId, Guid agentId, string agentName, string toolName, string toolCallId, bool success = true, string? errorMessage = null);
    Task BroadcastTypingIndicatorAsync(Guid channelId, Guid agentId, string agentName, bool isTyping);
    Task BroadcastUserPresenceAsync(Guid userId, string username, bool isOnline);
    Task BroadcastAgentStatusAsync(Guid channelId, Guid agentId, string agentName, string status);

    // DM events
    Task BroadcastDmMessageAddedAsync(Guid dmId, Message message);
    Task BroadcastDmAgentThinkingStartAsync(Guid dmId, Guid agentId, string agentName);
    Task BroadcastDmAgentThinkingEndAsync(Guid dmId, Guid agentId, string agentName);
    Task BroadcastDmAgentTextContentAsync(Guid dmId, Guid agentId, string agentName, string content, bool isDelta = true);
    Task BroadcastDmToolCallStartAsync(Guid dmId, Guid agentId, string agentName, string toolName, string toolCallId);
    Task BroadcastDmToolCallEndAsync(Guid dmId, Guid agentId, string agentName, string toolName, string toolCallId, bool success = true, string? errorMessage = null);
    Task BroadcastDmTypingIndicatorAsync(Guid dmId, Guid agentId, string agentName, bool isTyping);
    Task BroadcastDmAgentStatusAsync(Guid dmId, Guid agentId, string agentName, string status);
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

    // ── Channel events ──────────────────────────────────────────────

    public Task BroadcastMessageAddedAsync(Guid channelId, Message message)
    {
        var eventMessage = new MessageAddedEvent { Message = message };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting MESSAGE_ADDED to channel {ChannelId}", channelId);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastAgentThinkingStartAsync(Guid channelId, Guid agentId, string agentName)
    {
        var eventMessage = new AgentThinkingStartEvent
        {
            AgentId = agentId,
            AgentName = agentName
        };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting AGENT_THINKING_START to channel {ChannelId} for agent {AgentName}", channelId, agentName);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastAgentThinkingEndAsync(Guid channelId, Guid agentId, string agentName)
    {
        var eventMessage = new AgentThinkingEndEvent
        {
            AgentId = agentId,
            AgentName = agentName
        };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting AGENT_THINKING_END to channel {ChannelId} for agent {AgentName}", channelId, agentName);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastAgentTextContentAsync(Guid channelId, Guid agentId, string agentName, string content, bool isDelta = true)
    {
        var eventMessage = new AgentTextContentEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            Content = content,
            IsDelta = isDelta
        };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogTrace("Broadcasting AGENT_TEXT_CONTENT to channel {ChannelId} for agent {AgentName}", channelId, agentName);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastToolCallStartAsync(Guid channelId, Guid agentId, string agentName, string toolName, string toolCallId)
    {
        var eventMessage = new ToolCallStartEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            ToolName = toolName,
            ToolCallId = toolCallId
        };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting TOOL_CALL_START to channel {ChannelId} for tool {ToolName}", channelId, toolName);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastToolCallEndAsync(Guid channelId, Guid agentId, string agentName, string toolName, string toolCallId, bool success = true, string? errorMessage = null)
    {
        var eventMessage = new ToolCallEndEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            ToolName = toolName,
            ToolCallId = toolCallId,
            Success = success,
            ErrorMessage = errorMessage
        };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting TOOL_CALL_END to channel {ChannelId} for tool {ToolName}", channelId, toolName);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastTypingIndicatorAsync(Guid channelId, Guid agentId, string agentName, bool isTyping)
    {
        var eventMessage = new TypingIndicatorEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            IsTyping = isTyping
        };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogTrace("Broadcasting TYPING_INDICATOR to channel {ChannelId} for agent {AgentName}: {IsTyping}", channelId, agentName, isTyping);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastUserPresenceAsync(Guid userId, string username, bool isOnline)
    {
        var eventMessage = new UserPresenceEvent
        {
            UserId = userId,
            Username = username,
            IsOnline = isOnline
        };

        _logger.LogDebug("Broadcasting USER_PRESENCE for user {Username}: {IsOnline}", username, isOnline);

        // Broadcast to ALL connected clients (not channel-specific)
        return _channelHubContext.Clients.All.SendAsync("event", eventMessage);
    }

    public Task BroadcastAgentStatusAsync(Guid channelId, Guid agentId, string agentName, string status)
    {
        var eventMessage = new AgentStatusEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            Status = status
        };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting AGENT_STATUS to channel {ChannelId} for agent {AgentName}: {Status}", channelId, agentName, status);

        return _channelHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    // ── DM events ───────────────────────────────────────────────────

    public Task BroadcastDmMessageAddedAsync(Guid dmId, Message message)
    {
        var eventMessage = new MessageAddedEvent { Message = message };
        var groupName = GetDmGroupName(dmId);

        _logger.LogDebug("Broadcasting MESSAGE_ADDED to DM {DmId}", dmId);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmAgentThinkingStartAsync(Guid dmId, Guid agentId, string agentName)
    {
        var eventMessage = new AgentThinkingStartEvent
        {
            AgentId = agentId,
            AgentName = agentName
        };
        var groupName = GetDmGroupName(dmId);

        _logger.LogDebug("Broadcasting AGENT_THINKING_START to DM {DmId} for agent {AgentName}", dmId, agentName);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmAgentThinkingEndAsync(Guid dmId, Guid agentId, string agentName)
    {
        var eventMessage = new AgentThinkingEndEvent
        {
            AgentId = agentId,
            AgentName = agentName
        };
        var groupName = GetDmGroupName(dmId);

        _logger.LogDebug("Broadcasting AGENT_THINKING_END to DM {DmId} for agent {AgentName}", dmId, agentName);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmAgentTextContentAsync(Guid dmId, Guid agentId, string agentName, string content, bool isDelta = true)
    {
        var eventMessage = new AgentTextContentEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            Content = content,
            IsDelta = isDelta
        };
        var groupName = GetDmGroupName(dmId);

        _logger.LogTrace("Broadcasting AGENT_TEXT_CONTENT to DM {DmId} for agent {AgentName}", dmId, agentName);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmToolCallStartAsync(Guid dmId, Guid agentId, string agentName, string toolName, string toolCallId)
    {
        var eventMessage = new ToolCallStartEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            ToolName = toolName,
            ToolCallId = toolCallId
        };
        var groupName = GetDmGroupName(dmId);

        _logger.LogDebug("Broadcasting TOOL_CALL_START to DM {DmId} for tool {ToolName}", dmId, toolName);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmToolCallEndAsync(Guid dmId, Guid agentId, string agentName, string toolName, string toolCallId, bool success = true, string? errorMessage = null)
    {
        var eventMessage = new ToolCallEndEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            ToolName = toolName,
            ToolCallId = toolCallId,
            Success = success,
            ErrorMessage = errorMessage
        };
        var groupName = GetDmGroupName(dmId);

        _logger.LogDebug("Broadcasting TOOL_CALL_END to DM {DmId} for tool {ToolName}", dmId, toolName);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmTypingIndicatorAsync(Guid dmId, Guid agentId, string agentName, bool isTyping)
    {
        var eventMessage = new TypingIndicatorEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            IsTyping = isTyping
        };
        var groupName = GetDmGroupName(dmId);

        _logger.LogTrace("Broadcasting TYPING_INDICATOR to DM {DmId} for agent {AgentName}: {IsTyping}", dmId, agentName, isTyping);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    public Task BroadcastDmAgentStatusAsync(Guid dmId, Guid agentId, string agentName, string status)
    {
        var eventMessage = new AgentStatusEvent
        {
            AgentId = agentId,
            AgentName = agentName,
            Status = status
        };
        var groupName = GetDmGroupName(dmId);

        _logger.LogDebug("Broadcasting AGENT_STATUS to DM {DmId} for agent {AgentName}: {Status}", dmId, agentName, status);

        return _dmHubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string GetChannelGroupName(Guid channelId) => $"channel_{channelId}";
    private static string GetDmGroupName(Guid dmId) => $"dm_{dmId}";
}
