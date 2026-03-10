using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class AgentInfoPromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data) => true;

    public string GetContent(AgentRunData data)
    {
        return $"""
## Your (agent) information
Username: {data.Agent.Info.Username}
Powered by model: {data.Agent.Info.Model}
Description: {data.Agent.Info.Description}
User prompt: {data.Agent.Info.Prompt}

""";
    }
}
