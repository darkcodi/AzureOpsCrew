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
    public bool IsHidden { get; set; }

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

    public static AocLlmChatMessage FromContent(AocAiContent content, ChatRole role, string authorName, DateTime createdAt)
    {
        var contentDto = AocAiContentDto.FromAocAiContent(content);

        // ToDo: review what should be hidden from LLM and what's not
        var isHidden = contentDto.ContentType == LlmMessageContentType.UsageContent;

        return new AocLlmChatMessage
        {
            Id = Guid.NewGuid(),
            Role = role,
            AuthorName = authorName,
            CreatedAt = createdAt,
            ContentDto = contentDto,
            IsHidden = isHidden,
        };
    }

    public LlmChatMessage ToDomain(Guid agentId, Guid threadId, Guid runId)
    {
        return new LlmChatMessage
        {
            Id = Id,
            AgentId = agentId,
            ThreadId = threadId,
            RunId = runId,
            IsHidden = IsHidden,
            Role = Role,
            AuthorName = AuthorName,
            CreatedAt = CreatedAt,
            ContentType = Enum.Parse<LlmMessageContentType>(ContentDto.ContentType.ToString()),
            ContentJson = ContentDto.Content,
        };
    }

    public static AocLlmChatMessage FromDomain(LlmChatMessage domainMessage)
    {
        var contentDto = new AocAiContentDto
        {
            Content = domainMessage.ContentJson,
            ContentType = Enum.Parse<LlmMessageContentType>(domainMessage.ContentType.ToString()),
        };
        return new AocLlmChatMessage
        {
            Id = domainMessage.Id,
            Role = domainMessage.Role,
            AuthorName = domainMessage.AuthorName,
            CreatedAt = domainMessage.CreatedAt,
            ContentDto = contentDto,
            IsHidden = domainMessage.IsHidden,
        };
    }
}
