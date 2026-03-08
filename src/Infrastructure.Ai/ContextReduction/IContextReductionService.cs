using AzureOpsCrew.Domain.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

public interface IContextReductionService
{
    ContextReductionResult ReduceIfNeeded(
        List<ChatMessage> allMessages,
        string? systemPrompt,
        IReadOnlyList<ToolDeclaration> tools,
        string modelId);
}
