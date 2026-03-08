using AzureOpsCrew.Domain.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Serilog;

namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

public sealed class ContextReductionService : IContextReductionService
{
    private readonly ContextReductionSettings _settings;

    public ContextReductionService(IOptions<ContextReductionSettings> settings)
    {
        _settings = settings.Value;
    }

    public ContextReductionResult ReduceIfNeeded(
        List<ChatMessage> allMessages,
        string? systemPrompt,
        IReadOnlyList<ToolDeclaration> tools,
        string modelId)
    {
        var charsPerToken = _settings.CharsPerToken;
        var safetyMargin = _settings.SafetyMargin;

        // 1. Resolve context window
        var contextWindow = ModelContextWindowLookup.GetContextWindowSize(modelId)
                            ?? _settings.FallbackContextWindowSize;

        // 2. Calculate max input budget = contextWindow - reserved output tokens
        var reservedOutput = Math.Max(_settings.MinReservedOutputTokens, (int)(contextWindow * 0.20));
        var maxInputBudget = contextWindow - reservedOutput;

        // 3. Estimate fixed overhead
        var systemPromptTokens = TokenEstimator.EstimateSystemPromptTokens(systemPrompt, charsPerToken, safetyMargin);
        var toolSchemaTokens = TokenEstimator.EstimateToolSchemasTokens(tools, charsPerToken, safetyMargin);
        var fixedOverhead = systemPromptTokens + toolSchemaTokens;

        // 4. Estimate message tokens
        var messageTokens = TokenEstimator.EstimateMessagesTokens(allMessages, charsPerToken, safetyMargin);

        var totalEstimated = fixedOverhead + messageTokens;
        var softThreshold = (int)(maxInputBudget * _settings.SoftThresholdPercent);

        // 5. If under soft threshold, no reduction needed
        if (totalEstimated <= softThreshold)
        {
            Log.Debug(
                "[CONTEXT] No reduction needed — estimated {EstimatedTokens} tokens, budget {Budget}, threshold {Threshold} ({ThresholdPercent:P0}), model {Model}",
                totalEstimated, maxInputBudget, softThreshold, _settings.SoftThresholdPercent, modelId);

            return new ContextReductionResult(
                Messages: allMessages,
                Stage1Applied: false,
                OriginalMessageTokens: messageTokens,
                ReducedMessageTokens: messageTokens,
                RemovedMessageCount: 0,
                FinalEstimatedInputTokens: totalEstimated,
                MaxInputBudget: maxInputBudget);
        }

        // 6. Stage 1: Remove older tool-call groups (atomic: assistant + tool results)
        Log.Information(
            "[CONTEXT] Stage 1 triggered — estimated {EstimatedTokens} tokens, budget {Budget}, threshold {Threshold} ({ThresholdPercent:P0}), model {Model}",
            totalEstimated, maxInputBudget, softThreshold, _settings.SoftThresholdPercent, modelId);

        var classified = ChatMessageClassifier.ClassifyAndGroupMessages(allMessages);

        // 7. Calculate effective tool budget: the smaller of the configured budget or
        //    the remaining room after conversation + fixed overhead.
        //    This ensures tool groups are actually removed when conversation fills the context.
        var conversationTokens = TokenEstimator.EstimateMessagesTokens(classified.Conversation, charsPerToken, safetyMargin);
        var remainingRoom = Math.Max(0, maxInputBudget - fixedOverhead - conversationTokens);
        var effectiveToolBudget = Math.Min(_settings.RecentToolBudgetTokens, remainingRoom);

        Log.Debug(
            "[CONTEXT] Tool budget: configured {Configured}, remaining room {Remaining}, effective {Effective}, conversation tokens {ConversationTokens}",
            _settings.RecentToolBudgetTokens, remainingRoom, effectiveToolBudget, conversationTokens);

        // 8. Walk newest→oldest tool-call groups, keep within effective budget
        var keptGroups = SelectRecentToolGroups(classified.ToolCallGroups, effectiveToolBudget, charsPerToken, safetyMargin);

        // 9. Merge conversation + kept tool-call groups and re-sort
        var reducedMessages = new List<ChatMessage>(classified.Conversation.Count + keptGroups.Count * 2);
        reducedMessages.AddRange(classified.Conversation);
        foreach (var group in keptGroups)
            reducedMessages.AddRange(group.AllMessages);
        reducedMessages.Sort((a, b) => Nullable.Compare(a.CreatedAt, b.CreatedAt));

        var removedCount = allMessages.Count - reducedMessages.Count;
        var reducedMessageTokens = TokenEstimator.EstimateMessagesTokens(reducedMessages, charsPerToken, safetyMargin);
        var finalEstimated = fixedOverhead + reducedMessageTokens;

        Log.Information(
            "[CONTEXT] Stage 1 complete — removed {RemovedCount} messages, tokens {Before} → {After}, final total {FinalTotal}, budget {Budget}",
            removedCount, messageTokens, reducedMessageTokens, finalEstimated, maxInputBudget);

        if (finalEstimated > maxInputBudget)
        {
            Log.Warning(
                "[CONTEXT] Still over budget after Stage 1 — estimated {EstimatedTokens} tokens, budget {Budget}, model {Model}",
                finalEstimated, maxInputBudget, modelId);
        }

        return new ContextReductionResult(
            Messages: reducedMessages,
            Stage1Applied: true,
            OriginalMessageTokens: messageTokens,
            ReducedMessageTokens: reducedMessageTokens,
            RemovedMessageCount: removedCount,
            FinalEstimatedInputTokens: finalEstimated,
            MaxInputBudget: maxInputBudget);
    }

    /// <summary>
    /// Selects the newest tool-call groups that fit within the given token budget.
    /// Groups are kept or removed atomically — an assistant message with tool_calls is never
    /// separated from its matching tool-result messages.
    /// At least one group is always kept if any groups exist.
    /// </summary>
    private static List<ToolCallGroup> SelectRecentToolGroups(
        List<ToolCallGroup> toolCallGroups,
        int budget,
        double charsPerToken,
        double safetyMargin)
    {
        if (toolCallGroups.Count == 0)
            return [];

        var accumulated = 0;
        var selected = new List<ToolCallGroup>();

        // Walk from newest to oldest (groups are ordered chronologically, so reverse)
        for (var i = toolCallGroups.Count - 1; i >= 0; i--)
        {
            var group = toolCallGroups[i];
            var groupTokens = group.EstimateTokens(charsPerToken, safetyMargin);

            if (accumulated + groupTokens > budget && selected.Count > 0)
                break;

            // Always keep at least one group
            selected.Add(group);
            accumulated += groupTokens;
        }

        return selected;
    }
}
