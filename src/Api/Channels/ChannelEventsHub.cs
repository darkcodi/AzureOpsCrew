using Microsoft.AspNetCore.SignalR;

namespace AzureOpsCrew.Api.Channels;

/// <summary>
/// SignalR hub for real-time channel events.
/// Clients can join specific channel groups to receive events
/// for messages, agent thinking, tool calls, and typing indicators.
/// </summary>
public class ChannelEventsHub : Hub
{
    private readonly ILogger<ChannelEventsHub> _logger;

    public ChannelEventsHub(ILogger<ChannelEventsHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join a channel group to receive events for that channel.
    /// </summary>
    /// <param name="channelId">The ID of the channel to join.</param>
    public async Task JoinChannel(Guid channelId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"channel_{channelId}");
        _logger.LogInformation("Connection {ConnectionId} joined channel {ChannelId}",
            Context.ConnectionId, channelId);
    }

    /// <summary>
    /// Leave a channel group to stop receiving events for that channel.
    /// </summary>
    /// <param name="channelId">The ID of the channel to leave.</param>
    public async Task LeaveChannel(Guid channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"channel_{channelId}");
        _logger.LogInformation("Connection {ConnectionId} left channel {ChannelId}",
            Context.ConnectionId, channelId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
