using Front.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Front.Services;

/// <summary>
/// Reusable SignalR hub client for receiving real-time chat events.
/// Works with both ChannelEventsHub and DmEventsHub.
/// </summary>
public sealed class ChatHubClient : IAsyncDisposable
{
    private readonly string _joinMethod;
    private readonly string _leaveMethod;
    private readonly ILogger _logger;

    private HubConnection? _connection;
    private Guid? _currentGroupId;

    public event Action<ChatMessageDto>? MessageReceived;
    public event Action<ToolCallDto>? ToolCallReceived;

    private ChatHubClient(string joinMethod, string leaveMethod, ILogger logger)
    {
        _joinMethod = joinMethod;
        _leaveMethod = leaveMethod;
        _logger = logger;
    }

    public static ChatHubClient ForChannel(ILoggerFactory loggerFactory) =>
        new("JoinChannel", "LeaveChannel", loggerFactory.CreateLogger<ChatHubClient>());

    public static ChatHubClient ForDm(ILoggerFactory loggerFactory) =>
        new("JoinDm", "LeaveDm", loggerFactory.CreateLogger<ChatHubClient>());

    public async Task ConnectAsync(string hubUrl, Guid groupId)
    {
        await DisconnectAsync();

        _currentGroupId = groupId;

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Transports =
                    Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                    Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents;
            })
            .WithAutomaticReconnect(new ExponentialBackoffRetryPolicy())
            .Build();

        _connection.On<ChannelEvent>("event", HandleEvent);

        _connection.Reconnecting += ex =>
        {
            _logger.LogWarning(ex, "SignalR reconnecting...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += async _ =>
        {
            _logger.LogInformation("SignalR reconnected, re-joining group");
            await JoinGroupAsync();
        };

        _connection.Closed += ex =>
        {
            if (ex != null)
                _logger.LogWarning(ex, "SignalR connection closed with error");
            else
                _logger.LogInformation("SignalR connection closed");
            return Task.CompletedTask;
        };

        try
        {
            await _connection.StartAsync();
            _logger.LogInformation("SignalR connected to {Url}", hubUrl);
            await JoinGroupAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start SignalR connection to {Url}", hubUrl);
        }
    }

    public async Task DisconnectAsync()
    {
        if (_connection is null) return;

        try
        {
            if (_connection.State == HubConnectionState.Connected)
                await LeaveGroupAsync();

            await _connection.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during SignalR disconnect");
        }
        finally
        {
            await _connection.DisposeAsync();
            _connection = null;
            _currentGroupId = null;
        }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    private void HandleEvent(ChannelEvent evt)
    {
        if (evt is MessageAddedEvent msg)
        {
            _logger.LogDebug("Received MESSAGE_ADDED for message {Id}", msg.Message.Id);
            MessageReceived?.Invoke(msg.Message);
        }
        else if (evt is ToolCallCompletedEvent toolEvt)
        {
            _logger.LogDebug("Received TOOL_CALL_COMPLETED for {ToolName} {CallId}", toolEvt.ToolName, toolEvt.CallId);
            var dto = new ToolCallDto
            {
                ToolName = toolEvt.ToolName,
                CallId = toolEvt.CallId,
                Args = toolEvt.Args,
                Result = toolEvt.Result,
                IsError = toolEvt.IsError,
                Timestamp = toolEvt.Timestamp,
            };
            ToolCallReceived?.Invoke(dto);
        }
    }

    private async Task JoinGroupAsync()
    {
        if (_connection?.State != HubConnectionState.Connected || !_currentGroupId.HasValue)
            return;

        try
        {
            await _connection.InvokeAsync(_joinMethod, _currentGroupId.Value);
            _logger.LogInformation("Joined group {GroupId}", _currentGroupId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join group {GroupId}", _currentGroupId.Value);
        }
    }

    private async Task LeaveGroupAsync()
    {
        if (_connection?.State != HubConnectionState.Connected || !_currentGroupId.HasValue)
            return;

        try
        {
            await _connection.InvokeAsync(_leaveMethod, _currentGroupId.Value);
            _logger.LogDebug("Left group {GroupId}", _currentGroupId.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to leave group {GroupId}", _currentGroupId.Value);
        }
    }

    /// <summary>
    /// Exponential backoff: 0s, 2s, 10s, 30s, then cap at 60s.
    /// </summary>
    private sealed class ExponentialBackoffRetryPolicy : IRetryPolicy
    {
        private static readonly TimeSpan[] Delays =
        [
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60)
        ];

        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            var index = Math.Min(retryContext.PreviousRetryCount, Delays.Length - 1);
            return Delays[index];
        }
    }
}
