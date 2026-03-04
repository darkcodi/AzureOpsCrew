using Front.Models;

namespace Front.Services;

/// <summary>
/// Centralized state management for the chat application.
/// </summary>
public class ChatState
{
    private UserDto? _currentUser;
    private ServerInfoDto? _selectedServer = ServerInfoDto.GetDefaultServers().LastOrDefault();
    private ChannelDto? _selectedChannel = null;
    private DmChannelDto? _selectedDm;
    private List<ServerInfoDto> _servers = ServerInfoDto.GetDefaultServers();
    private List<ChannelDto> _channels = [];
    private bool _channelsLoaded = false;
    private List<DmChannelDto> _dms = [];
    private bool _dmsLoaded = false;
    private List<AgentDto> _agents = [];
    private Dictionary<Guid, List<ChatMessageDto>> _channelMessages = [];
    private Dictionary<Guid, List<ChatMessageDto>> _dmMessages = [];
    private Dictionary<Guid, AgentDto> _typingAgents = [];

    // Current user
    public UserDto? CurrentUser
    {
        get => _currentUser;
        set
        {
            _currentUser = value;
            OnStateChanged();
        }
    }

    // Server selection
    public ServerInfoDto? SelectedServer
    {
        get => _selectedServer;
        set
        {
            _selectedServer = value;
            OnStateChanged();
        }
    }

    // Channel/DM selection
    public ChannelDto? SelectedChannel
    {
        get => _selectedChannel;
        set
        {
            _selectedChannel = value;
            _selectedDm = null;
            OnStateChanged();
        }
    }

    public DmChannelDto? SelectedDm
    {
        get => _selectedDm;
        set
        {
            _selectedDm = value;
            _selectedChannel = null;
            OnStateChanged();
        }
    }

    // Data collections
    public List<ServerInfoDto> Servers
    {
        get => _servers;
        set
        {
            _servers = value;
            OnStateChanged();
        }
    }

    public List<ChannelDto> Channels
    {
        get => _channels;
        set
        {
            _channels = value;
            OnStateChanged();
        }
    }

    public List<DmChannelDto> Dms
    {
        get => _dms;
        set
        {
            _dms = value;
            OnStateChanged();
        }
    }

    public List<AgentDto> Agents
    {
        get => _agents;
        set
        {
            _agents = value;
            OnStateChanged();
        }
    }

    // Event for state changes
    public event Action? OnChange;

    private void OnStateChanged() => OnChange?.Invoke();

    // Messages management
    public List<ChatMessageDto> GetMessages(Guid channelIdOrDmId, bool isDm = false)
    {
        var key = isDm ? channelIdOrDmId : channelIdOrDmId;
        var dict = isDm ? _dmMessages : _channelMessages;

        if (!dict.ContainsKey(key))
        {
            dict[key] = [];
        }

        return dict[key];
    }

    public void SetMessages(Guid channelIdOrDmId, List<ChatMessageDto> messages, bool isDm = false)
    {
        var dict = isDm ? _dmMessages : _channelMessages;
        dict[channelIdOrDmId] = messages;
        OnStateChanged();
    }

    public void AddMessage(Guid channelIdOrDmId, ChatMessageDto message, bool isDm = false)
    {
        var dict = isDm ? _dmMessages : _channelMessages;

        if (!dict.ContainsKey(channelIdOrDmId))
        {
            dict[channelIdOrDmId] = [];
        }

        dict[channelIdOrDmId].Add(message);
        OnStateChanged();
    }

    public void UpdateAgentTyping(Guid agentId, AgentDto agent, bool isTyping)
    {
        if (isTyping)
        {
            _typingAgents[agentId] = agent;
        }
        else
        {
            _typingAgents.Remove(agentId);
        }
        OnStateChanged();
    }

    public List<AgentDto> GetTypingAgents() => _typingAgents.Values.ToList();

    // Get visible channels for selected server
    public List<ChannelDto> GetVisibleChannels()
    {
        if (_selectedServer == null) return [];

        // For now, return all channels
        // TODO: Filter by server when backend supports servers
        return _channels;
    }

    // Get agents for current channel
    public List<AgentDto> GetChannelAgents()
    {
        if (_selectedChannel == null) return [];

        var agentIds = _selectedChannel.AgentIds.Select(id => Guid.TryParse(id, out var guid) ? guid : (Guid?)null)
                                                  .Where(g => g.HasValue)
                                                  .Select(g => g!.Value)
                                                  .ToHashSet();

        return _agents.Where(a => agentIds.Contains(a.Id)).ToList();
    }

    // Get online users (placeholder for now)
    public List<UserDto> GetOnlineUsers() => []; // TODO: Implement when presence is available

    // Initialize channels from backend
    public async Task LoadChannels(ChannelService channelService)
    {
        if (_channelsLoaded) return;

        var channels = await channelService.GetChannelsAsync();
        _channels = channels;

        if (_channels.Any() && _selectedChannel == null)
        {
            _selectedChannel = _channels.First();
        }

        _channelsLoaded = true;
        OnStateChanged();
    }

    /// <summary>
    /// Reload channels from the API (e.g. after creating a new channel).
    /// </summary>
    public async Task RefreshChannelsAsync(ChannelService channelService)
    {
        var selectedId = _selectedChannel?.Id;
        _channelsLoaded = false;
        await LoadChannels(channelService);
        if (selectedId.HasValue && _channels.Count > 0)
        {
            var found = _channels.FirstOrDefault(c => c.Id == selectedId.Value);
            if (found != null)
                _selectedChannel = found;
        }
        OnStateChanged();
    }

    // Initialize DMs from backend
    public async Task LoadDms(DmService dmService)
    {
        if (_dmsLoaded) return;

        var dms = await dmService.GetDmsAsync();
        _dms = dms;

        if (_dms.Any())
        {
            _selectedDm = _dms.First();
        }

        _dmsLoaded = true;
        OnStateChanged();
    }
}
