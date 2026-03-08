using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

public sealed record ContextReductionResult(
    IReadOnlyList<ChatMessage> Messages,
    bool Stage1Applied,
    int OriginalMessageTokens,
    int ReducedMessageTokens,
    int RemovedMessageCount,
    int FinalEstimatedInputTokens,
    int MaxInputBudget);
