using System.Text.Json;
using AzureOpsCrew.Domain.Chats;
using Microsoft.Extensions.AI;

namespace Worker.Models.Content;

public class AocLlmChatMessage
{
    public Guid Id { get; set; }
    public ChatRole Role { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public AocAiContent Content { get; set; } = new AocTextContent();

    public ChatMessage ToChatMessage()
    {
        var aiContent = Content?.ToAiContent();
        var aiContentList = aiContent == null
            ? new List<AIContent>()
            : new List<AIContent> { aiContent };
        return new ChatMessage(Role, aiContentList)
        {
            Role = Role,
            AuthorName = AuthorName,
            CreatedAt = CreatedAt,
        };
    }

    public static AocLlmChatMessage FromContent(AocAiContent content, ChatRole role, string? authorName = null)
    {
        return new AocLlmChatMessage
        {
            Id = Guid.NewGuid(),
            Role = role,
            AuthorName = authorName,
            CreatedAt = DateTime.UtcNow,
            Content = content,
        };
    }

    public LlmChatMessage ToDomain(Guid agentId, string runId)
    {
        return new LlmChatMessage
        {
            Id = Id,
            AgentId = agentId,
            RunId = runId,
            IsHidden = false,
            Role = Role,
            AuthorName = AuthorName,
            CreatedAt = CreatedAt,
            ContentJson = JsonSerializer.Serialize(Content),
        };
    }

    public static AocLlmChatMessage FromDomain(LlmChatMessage domainMessage)
    {
        var content = JsonSerializer.Deserialize<AocAiContent>(domainMessage.ContentJson) ?? new AocTextContent();
        return new AocLlmChatMessage
        {
            Id = domainMessage.Id,
            Role = domainMessage.Role,
            AuthorName = domainMessage.AuthorName,
            CreatedAt = domainMessage.CreatedAt,
            Content = content,
        };
    }
}
