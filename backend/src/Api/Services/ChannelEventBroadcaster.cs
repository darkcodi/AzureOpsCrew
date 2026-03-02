using AzureOpsCrew.Api.Channels;
using AzureOpsCrew.Api.Endpoints.Dtos.Channels;
using AzureOpsCrew.Domain.Chats;
using Microsoft.AspNetCore.SignalR;

namespace AzureOpsCrew.Api.Services;

/// <summary>
/// Interface for broadcasting channel events via SignalR.
/// </summary>
public interface IChannelEventBroadcaster
{
    Task BroadcastMessageAddedAsync(Guid channelId, Message message);
    Task BroadcastAgentThinkingStartAsync(Guid channelId, Guid agentId, string agentName);
    Task BroadcastAgentThinkingEndAsync(Guid channelId, Guid agentId, string agentName);
    Task BroadcastAgentTextContentAsync(Guid channelId, Guid agentId, string agentName, string content, bool isDelta = true);
    Task BroadcastToolCallStartAsync(Guid channelId, Guid agentId, string agentName, string toolName, string toolCallId);
    Task BroadcastToolCallEndAsync(Guid channelId, Guid agentId, string agentName, string toolName, string toolCallId, bool success = true, string? errorMessage = null);
    Task BroadcastTypingIndicatorAsync(Guid channelId, Guid agentId, string agentName, bool isTyping);
    Task BroadcastUserPresenceAsync(Guid userId, string username, bool isOnline);
}

/// <summary>
/// Singleton service that provides thread-safe broadcasting of channel events via SignalR.
/// </summary>
public class ChannelEventBroadcaster : IChannelEventBroadcaster
{
    private readonly IHubContext<ChannelEventsHub> _hubContext;
    private readonly ILogger<ChannelEventBroadcaster> _logger;

    public ChannelEventBroadcaster(
        IHubContext<ChannelEventsHub> hubContext,
        ILogger<ChannelEventBroadcaster> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task BroadcastMessageAddedAsync(Guid channelId, Message message)
    {
        var eventMessage = new MessageAddedEvent { Message = message };
        var groupName = GetChannelGroupName(channelId);

        _logger.LogDebug("Broadcasting MESSAGE_ADDED to channel {ChannelId}", channelId);

        return _hubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
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

        return _hubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
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

        return _hubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
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

        return _hubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
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

        return _hubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
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

        return _hubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
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

        return _hubContext.Clients.Group(groupName).SendAsync("event", eventMessage);
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
        return _hubContext.Clients.All.SendAsync("event", eventMessage);
    }

    private static string GetChannelGroupName(Guid channelId) => $"channel_{channelId}";
}
