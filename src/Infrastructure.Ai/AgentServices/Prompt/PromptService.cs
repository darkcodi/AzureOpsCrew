using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt;

public class PromptService
{
    private static readonly IReadOnlyList<IPromptChunk> PromptChunks = new List<IPromptChunk>
    {
        new GeneralPromptChunk(),
        new ToneAndStylePromptChunk(),
        new ProactivenessPromptChunk(),
        new DoingTasksPromptChunk(),
        new ToolUsagePolicyPromptChunk(),
        new SystemUnderstandingPromptChunk(),
        new ChatRulesPromptChunk(),
        new AgentInfoPromptChunk(),
        new ChatParticipantsPromptChunk(),
        new ChannelManagerPromptChunk(),
        new ChannelWorkerPromptChunk(),
        new FirstMessagePromptChunk(),
    };

    public string PreparePrompt(AgentRunData data)
    {
        var prompt = string.Empty;

        foreach (var chunk in PromptChunks)
        {
            if (chunk.ShouldBeAdded(data))
            {
                prompt += chunk.GetContent(data);
            }
        }

        return prompt;
    }
}
