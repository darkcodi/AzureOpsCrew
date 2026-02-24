using Temporalio.Activities;
using Worker.Models;

namespace Worker.Activities;

public class AgentActivities
{
    // Put [Activity] attributes on methods per .NET docs.

    public static class Options
    {
        public static readonly Temporalio.Workflows.ActivityOptions Llm = new()
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(2),
            // Add RetryPolicy tuned for transient LLM failures.
        };

        public static readonly Temporalio.Workflows.ActivityOptions Tool = new()
        {
            StartToCloseTimeout = TimeSpan.FromMinutes(1),
            HeartbeatTimeout = TimeSpan.FromSeconds(10), // important for cancellation
        };
    }

    [Activity]
    public Task<NextDecision> DecideNextAsync(AgentContext ctx) => throw new NotImplementedException();

    [Activity]
    public Task<ToolResult> CallMcpToolAsync(ToolCall call) => throw new NotImplementedException();

    [Activity]
    public Task<string> SynthesizeTimeoutAnswerAsync(AgentContext ctx) => throw new NotImplementedException();
}
