using System.Text.Json;
using AzureOpsCrew.Domain.Chats;
using Microsoft.Extensions.AI;

namespace Worker.Models.Content;

public class AocLlmChatMessage
{
    public Guid Id { get; set; }
    public ChatRole Role { get; set; }
    public string? AuthorName { get; set; }
    public DateTime CreatedAt { get; set; }
    public AocAiContentDto ContentDto { get; set; } = new AocAiContentDto();

    public ChatMessage ToChatMessage()
    {
        var aocAiContent = ContentDto?.ToAocAiContent();
        var aiContent = aocAiContent?.ToAiContent();
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
        var contentDto = AocAiContentDto.FromAocAiContent(content);
        return new AocLlmChatMessage
        {
            Id = Guid.NewGuid(),
            Role = role,
            AuthorName = authorName,
            CreatedAt = DateTime.UtcNow,
            ContentDto = contentDto,
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
            ContentJson = JsonSerializer.Serialize(ContentDto),
        };
    }

    public static AocLlmChatMessage FromDomain(LlmChatMessage domainMessage)
    {
        var content = JsonSerializer.Deserialize<AocAiContentDto>(domainMessage.ContentJson) ?? new AocAiContentDto();
        return new AocLlmChatMessage
        {
            Id = domainMessage.Id,
            Role = domainMessage.Role,
            AuthorName = domainMessage.AuthorName,
            CreatedAt = domainMessage.CreatedAt,
            ContentDto = content,
        };
    }
}
