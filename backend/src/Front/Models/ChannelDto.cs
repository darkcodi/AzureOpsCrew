namespace Front.Models;

public class ChannelDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string[] AgentIds { get; set; } = [];
    public DateTime DateCreated { get; set; }
    public List<AgentDto> Agents { get; set; } = [];

    public static List<ChannelDto> GetDefaultChannels() =>
    [
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000010"),
            Name = "General",
            Description = "General discussion channel",
            DateCreated = DateTime.Parse("2024-01-01T00:00:00Z"),
            AgentIds = ["00000000-0000-0000-0000-000000000100", "00000000-0000-0000-0000-000000000101", "00000000-0000-0000-0000-000000000102"],
        },
        new()
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000011"),
            Name = "Test",
            Description = "Test channel for experiments",
            DateCreated = DateTime.Parse("2024-01-02T00:00:00Z"),
            AgentIds = ["00000000-0000-0000-0000-000000000100"],
        }
    ];
}
