namespace Front.Models;

/// <summary>
/// Temporary server info until backend supports servers.
/// </summary>
public class ServerInfoDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public List<Guid> ChannelIds { get; set; } = [];

    public static List<ServerInfoDto> GetDefaultServers() =>
    [
        new()
        {
            Id = Guid.Empty,
            Name = "Main",
            Icon = "M",
            ChannelIds = []
        }
    ];
}
