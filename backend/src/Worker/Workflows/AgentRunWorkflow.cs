using Temporalio.Workflows;
using Worker.Activities;
using Worker.Extensions;
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

        var agent = await Workflow.ExecuteActivityAsync((AgentActivities a) => a.LoadAgentAsync(agentId), Options);
        var provider = await Workflow.ExecuteActivityAsync((AgentActivities a) => a.LoadProviderAsync(agent.ProviderId), Options);

        // If waiting on a question, ensure the answer matches
        if (input.PendingQuestionBefore is not null &&
            (input.Trigger.Source != TriggerSource.UserAnswer ||
             input.Trigger.AnswerToQuestionId != input.PendingQuestionBefore.QuestionId))
        {
            return new RunOutcome(RunOutcomeKind.Noop, null, input.PendingQuestionBefore);
        }

        var toolResults = new List<ToolResult>();
        var userText = input.Trigger.Text ?? "";

        const int maxSteps = 6;

        for (int step = 0; step < maxSteps; step++)
        {
            var decision = await Workflow.ExecuteActivityAsync(
                (AgentActivities a) => a.AgentThinkAsync(agent, provider, userText, "", toolResults),
                Options);

            if (decision.NeedUserQuestion is not null)
            {
                var q = new PendingQuestion(
                    QuestionId: $"q-{Workflow.UtcNow.ToUnixTimeMilliseconds()}",
                    Text: decision.NeedUserQuestion,
                    AskedAt: Workflow.UtcNow);

                return new RunOutcome(RunOutcomeKind.BlockedOnUser, null, q);
            }

            if (decision.ToolCalls.Count > 0)
            {
                foreach (var call in decision.ToolCalls)
                {
                    var res = await Workflow.ExecuteActivityAsync(
                        (AgentActivities a) => a.CallMcpAsync(call),
                        Options);
                    toolResults.Add(res);
                }
                continue;
            }

            if (decision.FinalAnswer is not null)
            {
                return new RunOutcome(RunOutcomeKind.Completed, decision.FinalAnswer, null);
            }
        }

        return new RunOutcome(
            RunOutcomeKind.Completed,
            "I hit my step budget. Tell me what to focus on next.",
            null);
    }
}
