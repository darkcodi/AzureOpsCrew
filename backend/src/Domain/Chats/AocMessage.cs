namespace AzureOpsCrew.Domain.Chats;

public class AocMessage
{
    public Guid Id { get; set; }
    public Guid ChatId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }

    public AocChat Chat { get; set; } = null!;
}
