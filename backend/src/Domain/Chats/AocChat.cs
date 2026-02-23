namespace AzureOpsCrew.Domain.Chats;

public class AocChat
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<AocMessage> Messages { get; set; } = [];
}
