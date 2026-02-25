using Microsoft.Extensions.AI;

namespace Worker.Models.Content;

public class AocLlmChatMessage
{
    public ChatRole Role { get; set; }
    public string? AuthorName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public AocAiContent? Content { get; set; }

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
            Role = role,
            AuthorName = authorName,
            CreatedAt = DateTime.UtcNow,
            Content = content,
        };
    }
}
