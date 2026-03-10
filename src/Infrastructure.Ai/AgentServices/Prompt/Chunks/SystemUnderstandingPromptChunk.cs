using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class SystemUnderstandingPromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data) => true;

    public string GetContent(AgentRunData data)
    {
        return """
## System understanding

1. PARALLEL EXECUTION
Remember that all agents run in parallel, so some agents can post in chat while you are thinking or calling tools.
Do not assume that the chat history will remain the same between the time you read it and the time you respond. Someone can post while you are doing other stuff.

2. NO WAIT FOR CHAT.
Do NOT use Wait tool to wait for new messages in the chat. Instead, use the SkipTurn tool to skip your turn.
The system will automatically give you a new turn when there are new messages in the chat, so there is no need to wait for them. Waiting for new messages can lead to unnecessary delays and missed opportunities to respond to the user or other agents in a timely manner.

""";
    }
}
