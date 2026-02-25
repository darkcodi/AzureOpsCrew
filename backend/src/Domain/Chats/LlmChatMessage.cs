using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.Chats;

public class LlmChatMessage
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string RunId { get; set; } = string.Empty;
    public ChatRole Role { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string ContentJson { get; set; } = string.Empty;
}
