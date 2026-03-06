using System.Text;

namespace AzureOpsCrew.Domain.Chats;

// user <-> user (User1Id, User2Id)
// user <-> agent (User1Id, Agent2Id or Agent1Id, User2Id)
// agent <-> agent (Agent1Id, Agent2Id)
public class DirectMessageChannel
{
    public Guid Id { get; set; }

    // first participant (either user or agent, but not both)
    public Guid? User1Id { get; set; }
    public Guid? Agent1Id { get; set; }

    // second participant (either user or agent, but not both)
    public Guid? User2Id { get; set; }
    public Guid? Agent2Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public string GetDmChannelName()
    {
        var sb = new StringBuilder();
        sb.Append("DirectMessage (DM) between ");

        if (User1Id != null)
        {
            sb.Append($"User (id: {User1Id})");
        }
        if (Agent1Id != null)
        {
            sb.Append($"Agent (id: {Agent1Id})");
        }

        sb.Append(" and ");

        if (User2Id != null)
        {
            sb.Append($"User (id: {User2Id})");
        }
        if (Agent2Id != null)
        {
            sb.Append($"Agent (id: {Agent2Id})");
        }

        return sb.ToString();
    }
}
