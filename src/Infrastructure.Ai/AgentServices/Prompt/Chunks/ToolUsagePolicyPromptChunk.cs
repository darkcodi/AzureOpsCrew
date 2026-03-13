using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class ToolUsagePolicyPromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data)
    {
        return data.Tools.Count > 0;
    }

    public string GetContent(AgentRunData data)
    {
        return """
## Tool usage policy
- Use tools extensively to help you with your tasks. You have access to many tools that can help you with searching, coding, devops, and more. Use them!
- Run all tools ONLY sequentially, not in parallel.

""";
    }
}
