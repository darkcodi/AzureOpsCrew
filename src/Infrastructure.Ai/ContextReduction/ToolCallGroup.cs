using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

/// <summary>
/// Represents an atomic group of messages that must be kept or removed together:
/// an assistant message containing tool_calls and all its matching tool-result messages.
/// This ensures the OpenAI API invariant is maintained: every assistant message with
/// tool_calls must be immediately followed by tool messages for each tool_call_id.
/// </summary>
public sealed record ToolCallGroup(
    ChatMessage AssistantMessage,
    List<ChatMessage> ToolResultMessages)
{
    /// <summary>
    /// Returns all messages in the group (assistant + tool results) in chronological order.
    /// </summary>
    public IEnumerable<ChatMessage> AllMessages
    {
        get
        {
            yield return AssistantMessage;
            foreach (var toolResult in ToolResultMessages)
                yield return toolResult;
        }
    }

    /// <summary>
    /// Estimates the total token cost of the entire group.
    /// </summary>
    public int EstimateTokens(double charsPerToken, double safetyMargin)
    {
        var tokens = TokenEstimator.EstimateMessageTokens(AssistantMessage, charsPerToken, safetyMargin);
        foreach (var toolResult in ToolResultMessages)
            tokens += TokenEstimator.EstimateMessageTokens(toolResult, charsPerToken, safetyMargin);
        return tokens;
    }

    /// <summary>
    /// The timestamp used for ordering (based on the assistant message).
    /// </summary>
    public DateTimeOffset? Timestamp => AssistantMessage.CreatedAt;
}

