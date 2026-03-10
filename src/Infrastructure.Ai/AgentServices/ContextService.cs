using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Orchestration;
using AzureOpsCrew.Domain.Utils;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using Microsoft.Extensions.AI;
using Serilog;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices;

public class ContextService
{
    public const int TokenThresholdLow = 40_000;
    public const int TokenThresholdHigh = 80_000;
    public const string TruncatedToolResponsePlaceholder = "[Truncated]";

    public IList<ChatMessage> PrepareContext(AgentRunData data)
    {
        var allMessages = GroupMessages(data);

        // For orchestrated channels, workers on a task get limited context
        if (data.Channel != null && data.Channel.IsOrchestrated
            && data.Trigger?.Kind == AgentTriggerKind.TaskAssigned
            && data.Channel.ManagerAgentId != data.Agent.Id)
        {
            // Worker gets only the last N channel messages as context (enough for awareness)
            const int workerContextMessageLimit = 10;
            if (allMessages.Count > workerContextMessageLimit)
            {
                allMessages = allMessages.Skip(allMessages.Count - workerContextMessageLimit).ToList();
            }
        }

        // Sanitize: ensure tool-result messages always follow an assistant message
        // with tool_calls. Truncation or interleaving can break this ordering.
        allMessages = SanitizeToolMessageOrdering(allMessages);

        if (ShouldTruncateToolResults(allMessages, TokenThresholdHigh))
        {
            TruncateToolResults(allMessages, TokenThresholdLow);
        }

        return allMessages;
    }

    private bool ShouldTruncateToolResults(List<ChatMessage> allMessages, int maxTokens)
    {
        var toolResultTokens = allMessages
            .SelectMany(m => m.Contents)
            .OfType<FunctionResultContent>()
            .Sum(c => TokenUtils.EstimateTokensCount(c.Result?.ToString() ?? string.Empty));

        Log.Information("Total tokens in tool results: {ToolResultTokens}. Max allowed tokens: {MaxTokens}.", toolResultTokens, maxTokens);
        return toolResultTokens > maxTokens;
    }

    private void TruncateToolResults(List<ChatMessage> allMessages, int maxTokens)
    {
        var currentTokens = 0;
        var trimmedTokens = 0;
        var trim = false;
        var placeholderTokens = TokenUtils.EstimateTokensCount(TruncatedToolResponsePlaceholder);

        for (int i = allMessages.Count - 1; i >= 0; i--)
        {
            var message = allMessages[i];
            var messageContentList = message.Contents;

            foreach (var content in messageContentList)
            {
                if (content is FunctionResultContent toolResult)
                {
                    var toolResultString = toolResult.Result?.ToString() ?? string.Empty;
                    var toolResultTokens = TokenUtils.EstimateTokensCount(toolResultString);
                    if (trim)
                    {
                        toolResult.Result = TruncatedToolResponsePlaceholder;
                        trimmedTokens += (toolResultTokens - placeholderTokens);
                        continue;
                    }

                    if (currentTokens + toolResultTokens > maxTokens)
                    {
                        trim = true;
                    }
                    else
                    {
                        currentTokens += toolResultTokens;
                    }
                }
            }
        }

        Log.Information("Truncated {TrimmedTokens} tokens from tool results to fit within the context window.", trimmedTokens);
    }

    /// <summary>
    /// Ensures every 'tool' role message is preceded by an 'assistant' message
    /// with FunctionCallContent, and every assistant with FunctionCallContent
    /// is followed by tool result messages. Handles issues from truncation
    /// or timestamp-based interleaving that can break these pairings.
    /// </summary>
    private static List<ChatMessage> SanitizeToolMessageOrdering(List<ChatMessage> messages)
    {
        var result = new List<ChatMessage>();
        int i = 0;

        while (i < messages.Count)
        {
            var msg = messages[i];

            if (msg.Role == ChatRole.Assistant && msg.Contents.Any(c => c is FunctionCallContent))
            {
                // Look ahead: the very next messages must be tool results
                int j = i + 1;
                while (j < messages.Count && messages[j].Role == ChatRole.Tool)
                {
                    j++;
                }

                if (j > i + 1)
                {
                    // Valid block: assistant(tool_calls) + tool results
                    for (int k = i; k < j; k++)
                        result.Add(messages[k]);
                }
                else
                {
                    // No tool results follow immediately — strip FunctionCallContent
                    var nonToolCallContents = msg.Contents
                        .Where(c => c is not FunctionCallContent)
                        .ToList();
                    if (nonToolCallContents.Count > 0)
                    {
                        result.Add(new ChatMessage(msg.Role, nonToolCallContents)
                        {
                            AuthorName = msg.AuthorName,
                            CreatedAt = msg.CreatedAt,
                        });
                    }

                    Log.Warning(
                        "[ContextService] Stripped unanswered tool_calls from assistant message at position {Position}",
                        i);
                }

                i = j;
            }
            else if (msg.Role == ChatRole.Tool)
            {
                // Orphaned tool message (no preceding assistant with tool_calls)
                Log.Warning(
                    "[ContextService] Removing orphaned tool message at position {Position}", i);
                i++;
            }
            else
            {
                result.Add(msg);
                i++;
            }
        }

        return result;
    }

    private static List<ChatMessage> GroupMessages(AgentRunData data)
    {
        // Group thoughts by ChatMessageId to maintain iteration boundaries,
        // then split within each group whenever the role changes.
        // This ensures tool-result messages (role=tool) are separate from
        // assistant messages (role=assistant) with tool_calls, which the
        // OpenAI / DeepSeek API requires.
        var thoughtsByChatMessageId = data.LlmThoughts
            .GroupBy(t => t.ChatMessageId)
            .Select(g => g.OrderBy(t => t.CreatedAt).Select(AocAgentThought.FromDomain).ToList())
            .ToList();

        var chatMessagesFromThoughts = new List<ChatMessage>();

        foreach (var group in thoughtsByChatMessageId)
        {
            ChatRole? currentRole = null;
            var currentContents = new List<AIContent>();
            string? currentAuthor = null;
            DateTime? currentTimestamp = null;

            foreach (var thought in group)
            {
                if (thought.IsHidden) continue; // Skip hidden thoughts (UsageContent etc.)

                var content = thought.ContentDto.ToAocAiContent()?.ToAiContent();
                if (content == null) continue;

                // Start a new ChatMessage whenever the role changes
                if (currentRole != null && thought.Role != currentRole)
                {
                    if (currentContents.Count > 0)
                    {
                        chatMessagesFromThoughts.Add(new ChatMessage(currentRole.Value, currentContents)
                        {
                            AuthorName = currentAuthor,
                            CreatedAt = currentTimestamp.HasValue
                                ? new DateTimeOffset(currentTimestamp.Value, TimeSpan.Zero)
                                : null,
                        });
                    }

                    currentContents = new List<AIContent>();
                    currentTimestamp = null;
                }

                currentRole = thought.Role;
                currentAuthor = thought.AuthorName;
                currentTimestamp ??= thought.CreatedAt;
                currentContents.Add(content);
            }

            // Emit the last accumulated message in this group
            if (currentRole != null && currentContents.Count > 0)
            {
                chatMessagesFromThoughts.Add(new ChatMessage(currentRole.Value, currentContents)
                {
                    AuthorName = currentAuthor,
                    CreatedAt = currentTimestamp.HasValue
                        ? new DateTimeOffset(currentTimestamp.Value, TimeSpan.Zero)
                        : null,
                });
            }
        }

        var llmThoughtIds = data.LlmThoughts.Select(t => t.Id).ToHashSet();
        var chatMessages = data.ChatMessages.Where(x => !llmThoughtIds.Contains(x.AgentThoughtId ?? Guid.Empty)).Select(m => m.ToChatMessage()).ToList();
        var allMessages = chatMessages.Concat(chatMessagesFromThoughts).OrderBy(x => x.CreatedAt).ToList();
        return allMessages;
    }
}
