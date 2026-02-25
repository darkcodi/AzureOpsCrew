using System.Text.Json;
using AzureOpsCrew.Domain.LLMOutputs;
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

        var agent = await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.LoadAgent(agentId), Options);
        var provider = await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.LoadProvider(agent.ProviderId), Options);

        var toolResults = new List<ToolResult>();
        var userText = input.Trigger.Text ?? "";

        const int maxSteps = 6;

        for (int step = 0; step < maxSteps; step++)
        {
            var decision = await Workflow.ExecuteActivityAsync(
                (LlmActivities a) => a.LlmThinkAsync(agent, provider, userText, "", toolResults),
                Options);

            var llmOutputs = ToLllmOutputs(input, decision);
            await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.SaveLlmOutputBulk(llmOutputs), Options);

            if (decision.ToolCallsRequest != null)
            {
                foreach (var call in decision.ToolCallsRequest.ToolCalls)
                {
                    var res = await Workflow.ExecuteActivityAsync(
                        (McpActivities a) => a.CallMcpAsync(call),
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
            new FinalAnswer("I hit my step budget. Tell me what to focus on next.", null), null);
    }

    private static List<LlmOutput> ToLllmOutputs(RunInput input, NextStepDecision decision)
    {
        var outputs = new List<LlmOutput>();
        if (decision.ToolCallsRequest != null)
        {
            if (decision.ToolCallsRequest.Text is not null)
            {
                outputs.Add(new LlmOutput(Guid.NewGuid(), input.RunId, decision.ToolCallsRequest.Text, null, null, null));
            }
            foreach (var toolCall in decision.ToolCallsRequest.ToolCalls)
            {
                outputs.Add(new LlmOutput(Guid.NewGuid(), input.RunId, null, JsonSerializer.Serialize(toolCall), null, null));
            }
        }
        else
        {
            outputs.Add(new LlmOutput(Guid.NewGuid(), input.RunId, decision.FinalAnswer?.Text, null, decision.FinalAnswer?.Usage?.InputTokenCount, decision.FinalAnswer?.Usage?.OutputTokenCount));
        }
        return outputs;
    }
}
