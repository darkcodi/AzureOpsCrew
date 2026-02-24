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

        // Load snapshot
        var snapshot = await Workflow.ExecuteActivityAsync(
            (AgentActivities a) => a.LoadSnapshotAsync(agentId),
            Options);

        // If waiting on a question, ensure the answer matches
        if (input.PendingQuestionBefore is not null &&
            (input.Trigger.Source != TriggerSource.UserAnswer ||
             input.Trigger.AnswerToQuestionId != input.PendingQuestionBefore.QuestionId))
        {
            return new RunOutcome(RunOutcomeKind.Noop, null, input.PendingQuestionBefore, snapshot.MemorySummary);
        }

        var toolResults = new List<ToolResult>();
        var userText = input.Trigger.Text ?? "";

        const int maxSteps = 6;

        for (int step = 0; step < maxSteps; step++)
        {
            var decision = await Workflow.ExecuteActivityAsync(
                (AgentActivities a) => a.DecideNextAsync(agentId, userText, snapshot.MemorySummary, toolResults),
                Options);

            if (decision.NeedUserQuestion is not null)
            {
                var q = new PendingQuestion(
                    QuestionId: $"q-{Workflow.UtcNow.ToUnixTimeMilliseconds()}",
                    Text: decision.NeedUserQuestion,
                    AskedAt: Workflow.UtcNow);

                return new RunOutcome(RunOutcomeKind.BlockedOnUser, null, q, snapshot.MemorySummary);
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
                // Update snapshot (keep it small)
                var transcript = snapshot.RecentTranscript;
                transcript.Add(("user", userText));
                transcript.Add(("agent", decision.FinalAnswer));
                if (transcript.Count > 40) transcript.RemoveRange(0, transcript.Count - 40);

                var updated = snapshot with
                {
                    RecentTranscript = transcript,
                    MemorySummary = string.IsNullOrWhiteSpace(snapshot.MemorySummary)
                        ? "Started."
                        : snapshot.MemorySummary
                };

                await Workflow.ExecuteActivityAsync(
                    (AgentActivities a) => a.SaveSnapshotAsync(updated),
                    Options);

                return new RunOutcome(RunOutcomeKind.Completed, decision.FinalAnswer, null, updated.MemorySummary);
            }
        }

        return new RunOutcome(
            RunOutcomeKind.Completed,
            "I hit my step budget. Tell me what to focus on next.",
            null,
            snapshot.MemorySummary);
    }
}
