using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Client;
using Temporalio.Worker;
using Worker.Activities;
using Worker.Workflows;

// Setup DI container
var services = new ServiceCollection();
services.AddDbContext<AzureOpsCrewContext>(options =>
    options.UseInMemoryDatabase("WorkerDb"));
services.AddTransient<AgentActivities>();

var serviceProvider = services.BuildServiceProvider();

// Create a client to localhost on "default" namespace
var client = await TemporalClient.ConnectAsync(new("localhost:7233"));

// Cancellation token to shutdown worker on ctrl+c
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    cts.Cancel();
    eventArgs.Cancel = true;
};

// Create an activity instance since we have instance activities. If we had
// all static activities, we could just reference those directly.
var activities = serviceProvider.GetRequiredService<AgentActivities>();

// Create worker with the activity and workflow registered
using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions("aoc-agent-task-queue")
        .AddActivity(activities.LoadSnapshotAsync)
        .AddActivity(activities.SaveSnapshotAsync)
        .AddActivity(activities.DecideNextAsync)
        .AddActivity(activities.CallMcpAsync)
        .AddActivity(activities.NotifyUserAsync)
        .AddWorkflow<AgentCoordinatorWorkflow>()
        .AddWorkflow<AgentRunWorkflow>()
        .AddWorkflow<CronTriggerWorkflow>()
);

// Run worker until cancelled
Console.WriteLine("Running worker");
try
{
    await worker.ExecuteAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Worker cancelled");
}
