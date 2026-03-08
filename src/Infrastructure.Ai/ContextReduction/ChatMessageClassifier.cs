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

    /// <summary>
    /// Classifies messages into conversation messages, tool-call groups (atomic units), and
    /// "dependent" tool-result messages that belong to conversation-classified mixed messages.
    ///
    /// The key invariant enforced here: an assistant message with tool_calls and all its
    /// matching tool-result messages are always grouped together as a <see cref="ToolCallGroup"/>.
    /// Groups are kept or removed atomically during context reduction, preventing the
    /// "tool_calls without matching tool messages" API error.
    /// </summary>
    public static ClassifiedMessages ClassifyAndGroupMessages(IReadOnlyList<ChatMessage> messages)
    {
        var conversation = new List<ChatMessage>();
        var toolCallGroups = new List<ToolCallGroup>();

        // 1. Build a lookup: CallId → tool-role message (for matching results to their calls)
        var toolResultsByCallId = new Dictionary<string, ChatMessage>(StringComparer.Ordinal);
        foreach (var msg in messages)
        {
            if (msg.Role != ChatRole.Tool) continue;
            foreach (var content in msg.Contents)
            {
                if (content is FunctionResultContent frc && !string.IsNullOrEmpty(frc.CallId))
                    toolResultsByCallId[frc.CallId] = msg;
            }
        }

        // Track which tool-result messages have been claimed by a group or conversation
        var claimedToolResults = new HashSet<ChatMessage>(ReferenceEqualityComparer.Instance);

        // 2. Process each message
        foreach (var message in messages)
        {
            // Skip tool-role messages in this pass — they'll be claimed by their parent
            if (message.Role == ChatRole.Tool)
                continue;

            if (IsToolRelatedMessage(message))
            {
                // Pure tool-call assistant message: group it with matching tool results
                var callIds = GetFunctionCallIds(message);
                var matchingResults = new List<ChatMessage>();

                foreach (var callId in callIds)
                {
                    if (toolResultsByCallId.TryGetValue(callId, out var toolResult)
                        && !claimedToolResults.Contains(toolResult))
                    {
                        matchingResults.Add(toolResult);
                        claimedToolResults.Add(toolResult);
                    }
                }

                toolCallGroups.Add(new ToolCallGroup(message, matchingResults));
            }
            else
            {
                // Conversation message — but if it's a mixed message (text + tool_calls),
                // its matching tool results must also be preserved as conversation
                conversation.Add(message);

                var callIds = GetFunctionCallIds(message);
                foreach (var callId in callIds)
                {
                    if (toolResultsByCallId.TryGetValue(callId, out var toolResult)
                        && !claimedToolResults.Contains(toolResult))
                    {
                        conversation.Add(toolResult);
                        claimedToolResults.Add(toolResult);
                    }
                }
            }
        }

        // 3. Any unclaimed tool-result messages (orphaned — no matching assistant message found)
        //    are added to conversation to preserve them safely
        foreach (var msg in messages)
        {
            if (msg.Role == ChatRole.Tool && !claimedToolResults.Contains(msg))
                conversation.Add(msg);
        }

        return new ClassifiedMessages(conversation, toolCallGroups);
    }

    /// <summary>
    /// Legacy classification that splits into flat lists. Still used by tests for individual message classification.
    /// </summary>
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

    /// <summary>
    /// Extracts all FunctionCallContent.CallIds from a message.
    /// </summary>
    internal static List<string> GetFunctionCallIds(ChatMessage message)
    {
        var callIds = new List<string>();
        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent fcc && !string.IsNullOrEmpty(fcc.CallId))
                callIds.Add(fcc.CallId);
        }
        return callIds;
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

/// <summary>
/// Result of classifying messages: conversation messages that must always be kept,
/// and tool-call groups that can be selectively removed (as atomic units).
/// </summary>
public sealed record ClassifiedMessages(
    List<ChatMessage> Conversation,
    List<ToolCallGroup> ToolCallGroups);

