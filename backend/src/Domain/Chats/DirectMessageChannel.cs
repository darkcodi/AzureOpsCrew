namespace AzureOpsCrew.Domain.Chats;

public class DirectMessageChannel
{
    public Guid Id { get; set; }
    public Guid? User1Id { get; set; }
    public Guid? User2Id { get; set; }
    public Guid? Agent1Id { get; set; }
    public Guid? Agent2Id { get; set; }
    public DateTime CreatedAt { get; set; }
}
