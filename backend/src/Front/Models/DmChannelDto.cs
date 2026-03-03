namespace Front.Models;

public class DmChannelDto
{
    public Guid Id { get; set; }
    public Guid? User1Id { get; set; }
    public Guid? User2Id { get; set; }
    public Guid? Agent1Id { get; set; }
    public Guid? Agent2Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public UserDto? OtherUser { get; set; }
    public AgentDto? OtherAgent { get; set; }
}
