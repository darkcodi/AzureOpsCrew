using Temporalio.Workflows;
using Worker.Extensions;
using Worker.Models;

namespace Worker.Workflows;

[Workflow]
public class CronTriggerWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(CronTriggerInput input)
    {
        var coordId = $"agent:{input.AgentId}";
        var coord = Workflow.GetExternalWorkflowHandle(coordId);

        var trigger = new TriggerEvent(
            TriggerId: $"cron-{Workflow.UtcNow.ToUnixTimeMilliseconds()}",
            Source: TriggerSource.Cron,
            AgentId: input.AgentId,
            Text: "scheduled tick");

        await coord.SignalAsync("EnqueueAsync", [trigger]);
    }
}
