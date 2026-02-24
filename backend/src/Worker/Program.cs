using FluentMigrator.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Temporalio.Client;
using Temporalio.Worker;
using Worker.Activities;
using Worker.Extensions;
using Worker.Workflows;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json",
        optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Setup DI container
var services = new ServiceCollection();
services.AddSingleton(configuration);
services.AddDatabase(configuration);
services.AddProviderFacades();
services.AddTransient<AgentActivities>();

// Build the service provider
var serviceProvider = services.BuildServiceProvider();

// Run migrations
using (var scope = serviceProvider.CreateScope())
{
    var migrator = scope.ServiceProvider.GetRequiredService<IMigrationRunner>();
    Log.Information("Running database migrations...");
    migrator.MigrateUp();
    Log.Information("Database migrations completed.");
}

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
        .AddActivity(activities.LoadAgentAsync)
        .AddActivity(activities.LoadProviderAsync)
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
Log.Information("Running worker. Press Ctrl+C to exit.");
try
{
    await worker.ExecuteAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Log.Information("Worker cancellation requested. Shutting down...");
}
