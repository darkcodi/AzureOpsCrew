using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices;

public class ContextService
{
    public IList<ChatMessage> PrepareContext(AgentRunData data)
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
