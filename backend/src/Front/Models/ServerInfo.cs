namespace Front.Models;

/// <summary>
/// Temporary server info until backend supports servers.
/// </summary>
public class ServerInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<Guid> ChannelIds { get; set; } = [];

    public static List<ServerInfo> GetDefaultServers() =>
    [
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Direct Messages",
            Icon = "💬",
            ChannelIds = []
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Name = "General",
            Icon = "G",
            ChannelIds = []
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Name = "Projects",
            Icon = "P",
            ChannelIds = []
        }
    ];
}
