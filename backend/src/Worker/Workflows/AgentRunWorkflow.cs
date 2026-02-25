using Temporalio.Workflows;
using Worker.Activities;
using Worker.Models;

namespace Worker.Workflows;

[Workflow]
public class AgentRunWorkflow
{
    private static readonly ActivityOptions Options = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new() { MaximumAttempts = 3 }
    };

    [WorkflowRun]
    public async Task<RunOutcome> RunAsync(RunInput input)
    {
        var agentId = input.AgentId;

        var agent = await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.LoadAgentAsync(agentId), Options);
        var provider = await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.LoadProviderAsync(agent.ProviderId), Options);

        var toolResults = new List<ToolResult>();
        var userText = input.Trigger.Text ?? "";

        const int maxSteps = 6;

        for (int step = 0; step < maxSteps; step++)
        {
            var output = await Workflow.ExecuteActivityAsync(
                (LlmActivities a) => a.LlmThinkAsync(agent, provider, userText, "", toolResults),
                Options);

            if (output.ToolCalls.Count > 0)
            {
                foreach (var call in output.ToolCalls)
                {
                    var res = await Workflow.ExecuteActivityAsync(
                        (McpActivities a) => a.CallMcpAsync(call),
                        Options);
                    toolResults.Add(res);
                }
                continue;
            }

            if (output.FinalAnswer is not null)
            {
                return new RunOutcome(RunOutcomeKind.Completed, output.FinalAnswer, null);
            }
        }

        return new RunOutcome(
            RunOutcomeKind.Completed,
            new FinalAnswer("I hit my step budget. Tell me what to focus on next.", null), null);
    }
}
