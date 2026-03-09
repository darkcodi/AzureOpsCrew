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

    private static List<ChatMessage> GroupMessages(AgentRunData data)
    {
        // Group thoughts by ChatMessageId to reconstruct proper multi-content messages
        // Thoughts with the same ChatMessageId belong to the same original message
        var thoughtGroups = new List<List<AocAgentThought>>();

        var thoughtsByChatMessageId = data.LlmThoughts
            .GroupBy(t => t.ChatMessageId)
            .Select(g => g.OrderBy(t => t.CreatedAt).Select(AocAgentThought.FromDomain).ToList())
            .ToList();

        // Add groups with proper ChatMessageId
        thoughtGroups.AddRange(thoughtsByChatMessageId);

        var chatMessagesFromThoughts = new List<ChatMessage>();
        foreach (var group in thoughtGroups)
        {
            var firstThought = group.First();
            var allContents = group
                .Select(t => t.ContentDto.ToAocAiContent()?.ToAiContent())
                .Where(c => c != null)
                .Cast<AIContent>()
                .ToList();

            chatMessagesFromThoughts.Add(new ChatMessage(firstThought.Role, allContents)
            {
                Role = firstThought.Role,
                AuthorName = firstThought.AuthorName,
                CreatedAt = new DateTimeOffset(firstThought.CreatedAt, TimeSpan.Zero),
            });
        }

        var llmThoughtIds = data.LlmThoughts.Select(t => t.Id).ToHashSet();
        var chatMessages = data.ChatMessages.Where(x => !llmThoughtIds.Contains(x.AgentThoughtId ?? Guid.Empty)).Select(m => m.ToChatMessage()).ToList();
        var allMessages = chatMessages.Concat(chatMessagesFromThoughts).OrderBy(x => x.CreatedAt).ToList();
        return allMessages;
    }
}
