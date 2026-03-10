using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class FirstMessagePromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data)
    {
        var isFirstMessage = data.ChatMessages.Count < 2;
        return isFirstMessage;
    }

    public string GetContent(AgentRunData data)
    {
        return """
IMPORTANT! This is the very beginning of the chat. You MUST call the GetMessages tool to get the latest messages in the chat.

""";
    }
}
