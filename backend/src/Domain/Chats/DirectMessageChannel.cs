namespace AzureOpsCrew.Domain.Chats;

public class DirectMessageChannel
{
    public Guid Id { get; set; }
    public string? User1Id { get; set; }
    public string? User2Id { get; set; }
    public string? Agent1Id { get; set; }
    public string? Agent2Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<AocMessage> Messages { get; set; } = [];
}
