using Front.Models;

namespace Front.Services;

/// <summary>
/// Centralized state management for the chat application.
/// </summary>
public class AppState : IDisposable
{
    public AppState()
    {
        _subscriptions =
        [
            SubscribeCurrentUser(OnStateChanged),
            SubscribeSelectedServer(OnStateChanged),
            SubscribeSelectedChannel(OnStateChanged),
            SubscribeSelectedDm(OnStateChanged),
            SubscribeServers(OnStateChanged),
            SubscribeChannels(OnStateChanged),
            SubscribeDms(OnStateChanged),
            SubscribeAgents(OnStateChanged)
        ];
    }

    // Current user
    private readonly Reactive<UserDto?> _currentUser = new();
    public UserDto? CurrentUser
    {
        get => _currentUser.Value;
        set => _currentUser.Value = value;
    }
    public IDisposable SubscribeCurrentUser(Action handler) => _currentUser.Subscribe(handler);

    // Server selection
    private readonly Reactive<ServerInfoDto?> _selectedServer = new();
    public ServerInfoDto? SelectedServer
    {
        get => _selectedServer.Value;
        set => _selectedServer.Value = value;
    }
    public IDisposable SubscribeSelectedServer(Action handler) => _selectedServer.Subscribe(handler);

    // Channel selection
    private readonly Reactive<ChannelDto?> _selectedChannel = new();
    public ChannelDto? SelectedChannel
    {
        get => _selectedChannel.Value;
        set => _selectedChannel.Value = value;
    }
    public IDisposable SubscribeSelectedChannel(Action handler) => _selectedChannel.Subscribe(handler);

    // DM selection
    private readonly Reactive<DmChannelDto?> _selectedDm = new();
    public DmChannelDto? SelectedDm
    {
        get => _selectedDm.Value;
        set => _selectedDm.Value = value;
    }
    public IDisposable SubscribeSelectedDm(Action handler) => _selectedDm.Subscribe(handler);

    // Servers list
    public readonly ReactiveList<ServerInfoDto> Servers = new(ServerInfoDto.GetDefaultServers());
    public IDisposable SubscribeServers(Action handler) => Servers.Subscribe(handler);

    // Channels list
    public readonly ReactiveList<ChannelDto> Channels = new();
    public IDisposable SubscribeChannels(Action handler) => Channels.Subscribe(handler);
    private bool _channelsLoaded = false;

    // DMs list
    public readonly ReactiveList<DmChannelDto> Dms = new();
    public IDisposable SubscribeDms(Action handler) => Dms.Subscribe(handler);
    private bool _dmsLoaded = false;

    // Agents list
    public readonly ReactiveList<AgentDto> Agents = new();
    public IDisposable SubscribeAgents(Action handler) => Agents.Subscribe(handler);
    private bool _agentsLoaded = false;

    // Event for state changes
    private List<IDisposable> _subscriptions = new();
    public event Action? OnChange;

    private void OnStateChanged() => OnChange?.Invoke();


    // Get visible channels for selected server
    public IEnumerable<ChannelDto> GetVisibleChannels()
    {
        if (SelectedServer == null) return [];

        // For now, return all channels
        // TODO: Filter by server when backend supports servers
        return Channels;
    }

    // Get agents for current channel
    public List<AgentDto> GetChannelAgents()
    {
        if (SelectedChannel == null) return [];

        var agentIds = SelectedChannel.AgentIds.Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
                                                  .Where(g => g.HasValue)
                                                  .Select(g => g!.Value)
                                                  .ToHashSet();

        return Agents.Where(a => agentIds.Contains(a.Id)).ToList();
    }

    // Get online users (placeholder for now)
    public List<UserDto> GetOnlineUsers() => []; // TODO: Implement when presence is available

    // Load channels from backend
    public async Task LoadChannels(ChannelService channelService, bool forceReload = false)
    {
        if (_channelsLoaded && !forceReload) return;

        var channels = await channelService.GetChannelsAsync();
        Channels.Clear();
        foreach (var channel in channels)
        {
            Channels.Add(channel);
        }

        if (Channels.Any() && SelectedChannel == null)
        {
            SelectedChannel = Channels.First();
        }

        _channelsLoaded = true;
        OnStateChanged();
    }

    // Load DMs from backend
    public async Task LoadDms(DmService dmService, bool forceReload = false)
    {
        if (_dmsLoaded && !forceReload) return;

        var dms = await dmService.GetDmsAsync();
        Dms.Clear();
        foreach (var dm in dms)
        {
            Dms.Add(dm);
        }

        if (Dms.Any() && SelectedDm == null)
        {
            SelectedDm = Dms.First();
        }

        _dmsLoaded = true;
        OnStateChanged();
    }

    public async Task LoadAgents(AgentService agentService, bool forceReload = false)
    {
        if (_agentsLoaded && !forceReload) return;

        var agents = await agentService.GetAgentsAsync();
        Agents.Clear();
        foreach (var agent in agents)
        {
            Agents.Add(agent);
        }

        _agentsLoaded = true;
        OnStateChanged();
    }

    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
    }
}
