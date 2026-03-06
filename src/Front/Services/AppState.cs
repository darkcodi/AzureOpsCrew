using Front.Models;

namespace Front.Services;

/// <summary>
/// Centralized state management for the chat application.
/// </summary>
public class AppState
{
    private readonly Reactive<UserDto?> _currentUser = new();
    private readonly Reactive<ServerInfoDto?> _selectedServer = new();
    private readonly Reactive<ChannelDto?> _selectedChannel = new();
    private readonly Reactive<DmChannelDto?> _selectedDm = new();
    private readonly Reactive<Guid?> _lastUsedChannel = new();
    private readonly Reactive<Guid?> _lastUsedDm = new();
    private readonly Reactive<AgentMindResponseDto?> _currentAgentMind = new();
    public readonly ReactiveList<ServerInfoDto> Servers = new(ServerInfoDto.GetDefaultServers());
    public readonly ReactiveList<ChannelDto> Channels = new();
    public readonly ReactiveList<DmChannelDto> Dms = new();
    public readonly ReactiveList<AgentDto> Agents = new();

    public UserDto? CurrentUser
    {
        get => _currentUser.Value;
        set => _currentUser.Value = value;
    }

    public ServerInfoDto? SelectedServer
    {
        get => _selectedServer.Value;
        set => _selectedServer.Value = value;
    }

    public ChannelDto? SelectedChannel
    {
        get => _selectedChannel.Value;
        set
        {
            _selectedChannel.Value = value;
            if (value != null)
            {
                _lastUsedChannel.Value = value.Id;
            }
        }
    }

    public DmChannelDto? SelectedDm
    {
        get => _selectedDm.Value;
        set
        {
            _selectedDm.Value = value;
            if (value != null)
            {
                _lastUsedDm.Value = value.Id;
            }
        }
    }

    public Guid? LastUsedChannel
    {
        get => _lastUsedChannel.Value;
        set => _lastUsedChannel.Value = value;
    }

    public Guid? LastUsedDm
    {
        get => _lastUsedDm.Value;
        set => _lastUsedDm.Value = value;
    }

    public AgentMindResponseDto? CurrentAgentMind
    {
        get => _currentAgentMind.Value;
        set => _currentAgentMind.Value = value;
    }

    public IDisposable SubscribeLastUsedChannel(Action handler) => _lastUsedChannel.Subscribe(handler);
    public IDisposable SubscribeLastUsedDm(Action handler) => _lastUsedDm.Subscribe(handler);

    public IDisposable SubscribeCurrentUser(Action handler) => _currentUser.Subscribe(handler);
    public IDisposable SubscribeSelectedServer(Action handler) => _selectedServer.Subscribe(handler);
    public IDisposable SubscribeSelectedChannel(Action handler) => _selectedChannel.Subscribe(handler);
    public IDisposable SubscribeSelectedDm(Action handler) => _selectedDm.Subscribe(handler);
    public IDisposable SubscribeServers(Action handler) => Servers.Subscribe(handler);
    public IDisposable SubscribeChannels(Action handler) => Channels.Subscribe(handler);
    public IDisposable SubscribeDms(Action handler) => Dms.Subscribe(handler);
    public IDisposable SubscribeAgents(Action handler) => Agents.Subscribe(handler);
    public IDisposable SubscribeCurrentAgentMind(Action handler) => _currentAgentMind.Subscribe(handler);

    // Load data from backend
    public async Task LoadChannels(ChannelService channelService, bool forceReload = false)
    {
        if (Channels.Any() && !forceReload) return;

        var channels = await channelService.GetChannelsAsync();
        Channels.ReplaceAll(channels);

        var selectedId = SelectedChannel?.Id;
        if (selectedId != null)
        {
            var updated = Channels.FirstOrDefault(c => c.Id == selectedId);
            if (updated != null)
                SelectedChannel = updated;
        }
    }

    public async Task LoadDms(DmService dmService, bool forceReload = false)
    {
        if (Dms.Any() && !forceReload) return;

        var dms = await dmService.GetDmsAsync();
        Dms.ReplaceAll(dms);
    }

    public async Task LoadAgents(AgentService agentService, bool forceReload = false)
    {
        if (Agents.Any() && !forceReload) return;

        var agents = await agentService.GetAgentsAsync();
        Agents.ReplaceAll(agents);
    }

    public async Task LoadAgentMindAsync(AgentService agentService, Guid agentId)
    {
        var mindData = await agentService.GetAgentMindAsync(agentId);
        CurrentAgentMind = mindData;
    }
}
