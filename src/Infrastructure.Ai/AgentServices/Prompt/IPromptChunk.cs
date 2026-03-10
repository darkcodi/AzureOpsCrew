using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt;

public interface IPromptChunk
{
    bool ShouldBeAdded(AgentRunData data);
    string GetContent(AgentRunData data);
}
