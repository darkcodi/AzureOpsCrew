using Temporalio.Workflows;
using Worker.Activities;
using Worker.Models;

namespace Worker.Workflows;

[Workflow]
public class AgentConversationWorkflow
{
    private readonly Queue<AskInput> _pending = new();
    private readonly Dictionary<string, AskResult> _done = new();

    // Keep this compact. Prefer a rolling summary + last N messages.
    private readonly List<(string Role, string Content)> _transcript = new();

    private string? _currentTurnId;
    private string _phase = "idle";
    private int _turnCount;

    [WorkflowRun]
    public async Task RunAsync(ConversationInit init)
    {
        while (true)
        {
            await Workflow.WaitConditionAsync(() => _pending.Count > 0);

            while (_pending.Count > 0)
            {
                var req = _pending.Dequeue();
                _currentTurnId = req.TurnId;
                _phase = "thinking";

                var result = await RunAgentTurnAsync(req);

                _done[req.TurnId] = result;
                _transcript.Add(("user", req.Text));
                _transcript.Add(("assistant", result.Answer));
                _turnCount++;

                _currentTurnId = null;
                _phase = "idle";
            }

            // Periodically checkpoint to avoid history blow-up.
            // Temporal recommends doing this from the main loop, not inside handlers.
            if (Workflow.AllHandlersFinished &&
                (Workflow.ContinueAsNewSuggested || Workflow.CurrentHistoryLength > 20_000))
            {
                throw Workflow.CreateContinueAsNewException(
                    (AgentConversationWorkflow wf) => wf.RunAsync(init));
            }
        }
    }

    // Caller wants an answer -> Update is the right primitive.
    [WorkflowUpdate]
    public async Task<AskResult> AskAsync(AskInput input)
    {
        _pending.Enqueue(input);

        // Block until this specific turn is completed. WaitConditionAsync is the intended tool.
        await Workflow.WaitConditionAsync(() => _done.ContainsKey(input.TurnId));

        return _done[input.TurnId];
    }

    [WorkflowQuery]
    public AgentStatus GetStatus() =>
        new(_currentTurnId, _phase, _turnCount);

    // Optional: cancellation, feedback, etc. as Signals (no return value).
    [WorkflowSignal]
    public async Task CancelCurrentAsync()
    {
        // You can implement cancellation by flipping state and using Activity cancellation tokens.
        _phase = "cancelling";
    }

    private async Task<AskResult> RunAgentTurnAsync(AskInput req)
    {
        // Everything that touches the outside world is an Activity:
        // - LLM calls
        // - MCP tool calls
        // - DB/memory writes
        // Workflow orchestrates the loop deterministically.

        var toolsUsed = new List<ToolTrace>();

        var ctx = new AgentContext(
            TranscriptTail: _transcript.TakeLast(20).ToList(),
            UserText: req.Text);

        const int maxSteps = 8;

        for (var step = 0; step < maxSteps; step++)
        {
            _phase = $"decide:{step}";

            var decision = await Workflow.ExecuteActivityAsync(
                (AgentActivities a) => a.DecideNextAsync(ctx),
                AgentActivities.Options.Llm);

            if (decision.FinalAnswer is not null)
            {
                _phase = "done";
                return new AskResult(req.TurnId, decision.FinalAnswer, toolsUsed);
            }

            foreach (var call in decision.ToolCalls)
            {
                _phase = $"tool:{call.Server}/{call.Tool}";

                var toolRes = await Workflow.ExecuteActivityAsync(
                    (AgentActivities a) => a.CallMcpToolAsync(call),
                    AgentActivities.Options.Tool);

                toolsUsed.Add(new ToolTrace(
                    call.Server, call.Tool, toolRes.Summary, toolRes.IsError));

                // Update context with small tool result (or references).
                ctx = ctx with { ToolResults = ctx.ToolResults.Append(toolRes).ToList() };
            }
        }

        // Fallback if the agent loops too long
        var answer = await Workflow.ExecuteActivityAsync(
            (AgentActivities a) => a.SynthesizeTimeoutAnswerAsync(ctx),
            AgentActivities.Options.Llm);

        return new AskResult(req.TurnId, answer, toolsUsed);
    }
}
