using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Front.Models;

namespace Front.Services;

/// <summary>
/// Wrapper around HubConnection for SignalR channel events.
/// </summary>
public class SignalRService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly ILogger<SignalRService> _logger;
    private readonly IConfiguration _configuration;

    public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;
    public HubConnectionState ConnectionState => _hubConnection?.State ?? HubConnectionState.Disconnected;

    // Events
    public event Func<ChatMessageDto, Task>? MessageAdded;
    public event Func<Guid, string, Task>? AgentThinkingStart;
    public event Func<Guid, string, Task>? AgentThinkingEnd;
    public event Func<Guid, string, string, bool, Task>? AgentTextContent;
    public event Func<Guid, string, string, string, Task>? ToolCallStart;
    public event Func<Guid, string, string, string, bool, string?, Task>? ToolCallEnd;
    public event Func<Guid, string, bool, Task>? TypingIndicator;
    public event Func<Guid, string, bool, Task>? UserPresence;
    public event Func<Guid, string, string, Task>? AgentStatus;

    public SignalRService(ILogger<SignalRService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public async Task StartAsync(string? accessToken = null)
    {
        if (_hubConnection != null)
        {
            _logger.LogWarning("SignalR connection already exists");
            return;
        }

        var baseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:5282";
        var hubUrl = $"{baseUrl}/channels/events";

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    options.AccessTokenProvider = () => Task.FromResult(accessToken)!;
                }
                options.SkipNegotiation = false;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets | Microsoft.AspNetCore.Http.Connections.HttpTransportType.ServerSentEvents;
            })
            .WithAutomaticReconnect(new[] { TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) })
            .ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Information))
            .Build();

        // Register event handlers
        _hubConnection.On<ChannelEvent>("event", async (channelEvent) =>
        {
            _logger.LogDebug("Received channel event: {EventType}", channelEvent.Type);

            switch (channelEvent)
            {
                case MessageAddedEvent msgAdded:
                    if (MessageAdded != null) await MessageAdded.Invoke(msgAdded.Message);
                    break;
                case AgentThinkingStartEvent thinkingStart:
                    if (AgentThinkingStart != null) await AgentThinkingStart.Invoke(thinkingStart.AgentId, thinkingStart.AgentName);
                    break;
                case AgentThinkingEndEvent thinkingEnd:
                    if (AgentThinkingEnd != null) await AgentThinkingEnd.Invoke(thinkingEnd.AgentId, thinkingEnd.AgentName);
                    break;
                case AgentTextContentEvent textContent:
                    if (AgentTextContent != null) await AgentTextContent.Invoke(textContent.AgentId, textContent.AgentName, textContent.Content, textContent.IsDelta);
                    break;
                case ToolCallStartEvent toolStart:
                    if (ToolCallStart != null) await ToolCallStart.Invoke(toolStart.AgentId, toolStart.AgentName, toolStart.ToolName, toolStart.ToolCallId);
                    break;
                case ToolCallEndEvent toolEnd:
                    if (ToolCallEnd != null) await ToolCallEnd.Invoke(toolEnd.AgentId, toolEnd.AgentName, toolEnd.ToolName, toolEnd.ToolCallId, toolEnd.Success, toolEnd.ErrorMessage);
                    break;
                case TypingIndicatorEvent typing:
                    if (TypingIndicator != null) await TypingIndicator.Invoke(typing.AgentId, typing.AgentName, typing.IsTyping);
                    break;
                case UserPresenceEvent presence:
                    if (UserPresence != null) await UserPresence.Invoke(presence.UserId, presence.Username, presence.IsOnline);
                    break;
                case AgentStatusEvent status:
                    if (AgentStatus != null) await AgentStatus.Invoke(status.AgentId, status.AgentName, status.Status);
                    break;
            }
        });

        _hubConnection.Reconnecting += error =>
        {
            _logger.LogWarning("SignalR reconnecting: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        _hubConnection.Reconnected += connectionId =>
        {
            _logger.LogInformation("SignalR reconnected: {ConnectionId}", connectionId);
            return Task.CompletedTask;
        };

        _hubConnection.Closed += error =>
        {
            _logger.LogWarning("SignalR closed: {Error}", error?.Message);
            return Task.CompletedTask;
        };

        try
        {
            await _hubConnection.StartAsync();
            _logger.LogInformation("SignalR connection established");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting SignalR connection");
            throw;
        }
    }

    public async Task StopAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.StopAsync();
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }

    public async Task JoinChannelAsync(Guid channelId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("JoinChannel", channelId);
            _logger.LogDebug("Joined channel: {ChannelId}", channelId);
        }
    }

    public async Task LeaveChannelAsync(Guid channelId)
    {
        if (_hubConnection?.State == HubConnectionState.Connected)
        {
            await _hubConnection.InvokeAsync("LeaveChannel", channelId);
            _logger.LogDebug("Left channel: {ChannelId}", channelId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
