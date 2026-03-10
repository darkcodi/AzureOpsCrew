using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class ProactivenessPromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data) => true;

    public string GetContent(AgentRunData data)
    {
        return """
## Proactiveness
You are allowed to be proactive, but only when the user asks you to do something. You should strive to strike a balance between:
- Doing the right thing when asked, including taking actions and follow-up actions
- Not surprising the user with actions you take without asking
For example, if the user asks you how to approach something, you should do your best to answer their question first, and not immediately jump into taking actions.

""";
    }
}
