using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.Prompt.Chunks;

public class DoingTasksPromptChunk : IPromptChunk
{
    public bool ShouldBeAdded(AgentRunData data)
    {
        var agentUsername = data.Agent.Info.Username;
        var isManagerAgent = string.Equals(agentUsername, "manager", StringComparison.InvariantCultureIgnoreCase);
        return !isManagerAgent;
    }

    public string GetContent(AgentRunData data)
    {
        return """
## Doing tasks
The user will primarily request you perform tasks. This includes solving bugs, adding new functionality, refactoring code, explaining code, searching web, doing devops things, and more. For these tasks the following steps are recommended:
- Use the available search/exploration tools to understand the current situation and the user's query. You are encouraged to use the search tools extensively.
- Implement the solution using all tools available to you
- Verify the solution if possible. NEVER assume it's working after your changes. If there are tests available, run them. If there is a way to verify the correctness of your solution, do it.
- Tool results and user messages may include <system-reminder> tags. <system-reminder> tags contain useful information and reminders. They are NOT part of the user's provided input or the tool result.

""";
    }
}
