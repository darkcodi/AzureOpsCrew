using System.Text;

namespace AzureOpsCrew.Domain.Chats;

// user <-> user (User1Id, User2Id)
// user <-> agent (User1Id, Agent2Id or Agent1Id, User2Id)
// agent <-> agent (Agent1Id, Agent2Id)
public class DirectMessageChannel
{
    public Guid Id { get; set; }
    public Guid? User1Id { get; set; }
    public Guid? User2Id { get; set; }
    public Guid? Agent1Id { get; set; }
    public Guid? Agent2Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public string GetDmChannelName()
    {
        var sb = new StringBuilder();
        if (User1Id != null)
        {
            sb.Append($"User:{User1Id}");
        }
        if (Agent1Id != null)
        {
            sb.Append($"Agent:{Agent1Id}");
        }
        if (User2Id != null)
        {
            sb.Append($"User:{User2Id}");
        }
        if (Agent2Id != null)
        {
            sb.Append($"Agent:{Agent2Id}");
        }
        return sb.ToString();
    }
}
