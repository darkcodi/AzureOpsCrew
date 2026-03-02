using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.Chats;

public class Message
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime PostedAt { get; set; }

    // Sender: exactly one of AgentId or UserId should be set
    public Guid? AgentId { get; set; }
    public Guid? UserId { get; set; }

    // Destination: exactly one of ChannelId or DmId should be set
    public Guid? ChannelId { get; set; }
    public Guid? DmId { get; set; }

    public ChatMessage ToChatMessage()
    {
        var isAgentMessage = AgentId.HasValue;

        // ToDo: should we send other(!) agent's messages with Role=User or Role=Assistant to the LLM?
        var role = isAgentMessage ? ChatRole.Assistant : ChatRole.User;

        var aiContent = new TextContent(Text);
        var aiContentList = new List<AIContent> { aiContent };
        return new ChatMessage(role, aiContentList)
        {
            // ToDo: review what should be sent to LLM as AuthorName for agent messages and for user messages
            AuthorName = null, // AuthorName,

            CreatedAt = PostedAt,
        };
    }
}
