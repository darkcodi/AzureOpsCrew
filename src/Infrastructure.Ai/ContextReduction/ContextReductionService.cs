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

        // 6. Stage 1: Remove older tool messages
        Log.Information(
            "[CONTEXT] Stage 1 triggered — estimated {EstimatedTokens} tokens, budget {Budget}, threshold {Threshold} ({ThresholdPercent:P0}), model {Model}",
            totalEstimated, maxInputBudget, softThreshold, _settings.SoftThresholdPercent, modelId);

        var (conversation, toolRelated) = ChatMessageClassifier.ClassifyMessages(allMessages);

        // 7. Walk newest→oldest tool messages, keep within RecentToolBudgetTokens
        var keptToolMessages = SelectRecentToolMessages(toolRelated, charsPerToken, safetyMargin);

        // 8. Merge and re-sort
        var reducedMessages = new List<ChatMessage>(conversation.Count + keptToolMessages.Count);
        reducedMessages.AddRange(conversation);
        reducedMessages.AddRange(keptToolMessages);
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

    private List<ChatMessage> SelectRecentToolMessages(
        List<ChatMessage> toolMessages,
        double charsPerToken,
        double safetyMargin)
    {
        if (toolMessages.Count == 0)
            return [];

        var budget = _settings.RecentToolBudgetTokens;
        var accumulated = 0;
        var selected = new List<ChatMessage>();

        // Walk from newest to oldest
        for (var i = toolMessages.Count - 1; i >= 0; i--)
        {
            var msg = toolMessages[i];
            var msgTokens = TokenEstimator.EstimateMessageTokens(msg, charsPerToken, safetyMargin);

            if (accumulated + msgTokens > budget && selected.Count > 0)
                break;

            // Always keep at least one tool message
            selected.Add(msg);
            accumulated += msgTokens;
        }

        return selected;
    }
}
