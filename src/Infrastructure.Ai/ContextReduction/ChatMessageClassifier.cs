using Microsoft.Extensions.AI;

#pragma warning disable MEAI001

namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

public static class ChatMessageClassifier
{
    public static bool IsToolRelatedMessage(ChatMessage message)
    {
        // User messages are always conversation
        if (message.Role == ChatRole.User)
            return false;

        // System messages are always conversation
        if (message.Role == ChatRole.System)
            return false;

        // Tool-role messages are always tool-related
        if (message.Role == ChatRole.Tool)
            return true;

        // For assistant messages, check if ANY content is text (conversation).
        // Mixed messages (text + tool calls) are classified as conversation (v1 limitation).
        var hasTextContent = false;
        var hasAnyContent = false;

        foreach (var content in message.Contents)
        {
            hasAnyContent = true;

            if (content is TextContent)
            {
                hasTextContent = true;
                break;
            }
        }

        // If the message has text content, it's conversation (even if mixed with tool calls)
        if (hasTextContent)
            return false;

        // If there's no content at all, treat as conversation (preserve it)
        if (!hasAnyContent)
            return false;

        // All remaining content must be tool-related types
        foreach (var content in message.Contents)
        {
            if (!IsToolRelatedContent(content))
                return false;
        }

        return true;
    }

    public static (List<ChatMessage> Conversation, List<ChatMessage> ToolRelated) ClassifyMessages(
        IReadOnlyList<ChatMessage> messages)
    {
        var conversation = new List<ChatMessage>();
        var toolRelated = new List<ChatMessage>();

        foreach (var message in messages)
        {
            if (IsToolRelatedMessage(message))
                toolRelated.Add(message);
            else
                conversation.Add(message);
        }

        return (conversation, toolRelated);
    }

    private static bool IsToolRelatedContent(AIContent content)
    {
        return content is FunctionCallContent
            or FunctionResultContent
            or McpServerToolCallContent
            or McpServerToolResultContent
            or CodeInterpreterToolCallContent
            or CodeInterpreterToolResultContent
            or ImageGenerationToolCallContent
            or ImageGenerationToolResultContent
            or FunctionApprovalRequestContent
            or FunctionApprovalResponseContent
            or McpServerToolApprovalRequestContent
            or McpServerToolApprovalResponseContent
            or TextReasoningContent;
    }
}
