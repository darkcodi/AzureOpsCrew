using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.Chats;

public class LlmChatMessage
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid ThreadId { get; set; } = Guid.Empty;
    public Guid RunId { get; set; } = Guid.Empty;
    public bool IsHidden { get; set; }
    public ChatRole Role { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public LlmMessageContentType ContentType { get; set; } = LlmMessageContentType.None;
    public string ContentJson { get; set; } = string.Empty;
}
