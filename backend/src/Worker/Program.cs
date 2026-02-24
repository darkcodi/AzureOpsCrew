using Temporalio.Client;
using Temporalio.Worker;
using Worker.Activities;
using Worker.Workflows;

// Create a client to localhost on "default" namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

// Cancellation token to shutdown worker on ctrl+c
using var tokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    tokenSource.Cancel();
    eventArgs.Cancel = true;
};

// Create an activity instance since we have instance activities. If we had
// all static activities, we could just reference those directly.
var activities = new AgentActivities();

// Create worker with the activity and workflow registered
using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions("aoc-agent-task-queue")
        .AddActivity(activities.DecideNextAsync)
        .AddActivity(activities.CallMcpToolAsync)
        .AddActivity(activities.SynthesizeTimeoutAnswerAsync)
        .AddWorkflow<AgentConversationWorkflow>()
);

// Run worker until cancelled
Console.WriteLine("Running worker");
try
{
    await worker.ExecuteAsync(tokenSource.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Worker cancelled");
}
