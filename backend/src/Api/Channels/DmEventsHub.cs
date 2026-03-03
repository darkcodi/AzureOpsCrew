using Microsoft.AspNetCore.SignalR;

namespace AzureOpsCrew.Api.Channels;

/// <summary>
/// SignalR hub for real-time direct message events.
/// Clients can join specific DM groups to receive events
/// for messages, agent thinking, tool calls, and typing indicators.
/// </summary>
public class DmEventsHub : Hub
{
    private readonly ILogger<DmEventsHub> _logger;

    public DmEventsHub(ILogger<DmEventsHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join a DM group to receive events for that direct message channel.
    /// </summary>
    /// <param name="dmId">The ID of the DM channel to join.</param>
    public async Task JoinDm(Guid dmId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"dm_{dmId}");
        _logger.LogInformation("Connection {ConnectionId} joined DM {DmId}",
            Context.ConnectionId, dmId);
    }

    /// <summary>
    /// Leave a DM group to stop receiving events for that direct message channel.
    /// </summary>
    /// <param name="dmId">The ID of the DM channel to leave.</param>
    public async Task LeaveDm(Guid dmId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"dm_{dmId}");
        _logger.LogInformation("Connection {ConnectionId} left DM {DmId}",
            Context.ConnectionId, dmId);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("DM client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(exception, "DM client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation("DM client disconnected: {ConnectionId}", Context.ConnectionId);
        }
        await base.OnDisconnectedAsync(exception);
    }
}
