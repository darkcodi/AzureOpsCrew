using Front.Models;

namespace Front.Services;

public class AppInitializationService : IAsyncDisposable
{
    private bool _isInitialized = false;
    private readonly SignalRService _signalRService;
    private readonly UserService _userService;
    private readonly ChannelService _channelService;
    private readonly AgentService _agentService;
    private readonly ChatState _chatState;

    public AppInitializationService(
        SignalRService signalRService,
        UserService userService,
        ChannelService channelService,
        AgentService agentService,
        ChatState chatState)
    {
        _signalRService = signalRService;
        _userService = userService;
        _channelService = channelService;
        _agentService = agentService;
        _chatState = chatState;
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        // Load current user
        var currentUser = await _userService.GetCurrentUserAsync();
        if (currentUser != null)
        {
            _chatState.CurrentUser = currentUser;
        }

        // Load channels and agents
        await LoadDataAsync();

        // Connect to SignalR
        await _signalRService.StartAsync();

        // Subscribe to SignalR events
        _signalRService.MessageAdded += OnMessageAdded;
        _signalRService.AgentThinkingStart += OnAgentThinkingStart;
        _signalRService.AgentThinkingEnd += OnAgentThinkingEnd;
        _signalRService.AgentTextContent += OnAgentTextContent;
        _signalRService.TypingIndicator += OnTypingIndicator;

        _isInitialized = true;
    }

    private async Task LoadDataAsync()
    {
        var channels = await _channelService.GetChannelsAsync();
        _chatState.Channels = channels;

        var agents = await _agentService.GetAgentsAsync();
        _chatState.Agents = agents;

        // Populate agents in channels
        foreach (var channel in _chatState.Channels)
        {
            var agentIds = channel.AgentIds
                .Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
                .Where(g => g.HasValue)
                .Select(g => g!.Value)
                .ToHashSet();

            channel.Agents = _chatState.Agents.Where(a => agentIds.Contains(a.Id)).ToList();
        }

        // Select first channel if none selected
        if (_chatState.SelectedChannel == null && _chatState.Channels.Count > 0)
        {
            _chatState.SelectedChannel = _chatState.Channels[0];
        }
    }

    private Task OnMessageAdded(ChatMessageDto message)
    {
        if (message.ChannelId.HasValue)
        {
            _chatState.AddMessage(message.ChannelId.Value, message, isDm: false);
        }
        else if (message.DmId.HasValue)
        {
            _chatState.AddMessage(message.DmId.Value, message, isDm: true);
        }
        return Task.CompletedTask;
    }

    private Task OnAgentThinkingStart(Guid agentId, string agentName)
    {
        var agent = _chatState.Agents.FirstOrDefault(a => a.Id == agentId);
        if (agent != null)
        {
            agent.Status = "Thinking";
        }
        return Task.CompletedTask;
    }

    private Task OnAgentThinkingEnd(Guid agentId, string agentName)
    {
        var agent = _chatState.Agents.FirstOrDefault(a => a.Id == agentId);
        if (agent != null)
        {
            agent.Status = "Idle";
        }
        return Task.CompletedTask;
    }

    private Task OnAgentTextContent(Guid agentId, string agentName, string content, bool isDelta)
    {
        // Handle streaming content
        return Task.CompletedTask;
    }

    private Task OnTypingIndicator(Guid agentId, string agentName, bool isTyping)
    {
        var agent = _chatState.Agents.FirstOrDefault(a => a.Id == agentId);
        if (agent != null)
        {
            agent.IsTyping = isTyping;
            _chatState.UpdateAgentTyping(agentId, agent, isTyping);
        }
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _signalRService.MessageAdded -= OnMessageAdded;
        _signalRService.AgentThinkingStart -= OnAgentThinkingStart;
        _signalRService.AgentThinkingEnd -= OnAgentThinkingEnd;
        _signalRService.AgentTextContent -= OnAgentTextContent;
        _signalRService.TypingIndicator -= OnTypingIndicator;
        await _signalRService.StopAsync();
    }
}
