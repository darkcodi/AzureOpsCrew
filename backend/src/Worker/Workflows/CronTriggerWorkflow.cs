using Temporalio.Client;
using Temporalio.Client.Schedules;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Worker.Models;

namespace Worker.Workflows;

[Workflow]
public class CronTriggerWorkflow
{
    [WorkflowRun]
    public async Task RunAsync(CronTriggerInput input)
    {
        var coordId = AgentCoordinatorWorkflow.CoordinatorWorkflowId(input.AgentId);
        var coord = Workflow.GetExternalWorkflowHandle(coordId);

        var trigger = new TriggerEvent(
            TriggerId: Guid.NewGuid(),
            Source: TriggerSource.Cron,
            CreatedAt: DateTime.UtcNow,
            ThreadId: input.AgentId,
            RunId: Guid.NewGuid(),
            Text: "scheduled tick");

        await coord.SignalAsync("EnqueueAsync", [trigger]);
    }

    public static string CronWorkflowId(Guid agentId) => $"agent:{agentId}:cron";

    public static async Task EnsureCronScheduleAsync(TemporalClient client, Guid agentId)
    {
        var cronWorkflowId = CronWorkflowId(agentId);

        try
        {
            // Fires every 5 minutes (use Cron expressions if you prefer; intervals are simplest)
            await client.CreateScheduleAsync(
                cronWorkflowId,
                new Schedule(
                    Action: ScheduleActionStartWorkflow.Create(
                        (CronTriggerWorkflow wf) => wf.RunAsync(new CronTriggerInput(agentId)),
                        new(id: $"cron-trigger:{agentId}", taskQueue: "aoc-agent-task-queue")),
                    Spec: new()
                    {
                        Intervals = new List<ScheduleIntervalSpec> { new(Every: TimeSpan.FromMinutes(5)) }
                    }));
        }
        catch (ScheduleAlreadyRunningException)
        {
            // schedule already exists
        }
    }
}
