using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Worker.Activities;
using Worker.Models;

namespace Worker.Workflows;

[Workflow]
public class AgentCoordinatorWorkflow
{
    private readonly Queue<TriggerEvent> _triggersQueue = new();
    private readonly HashSet<Guid> _triggerIds = new();

    private AgentStatus _status = AgentStatus.Idle;
    private string? _currentRunId;
    private long _runNumber;

    private readonly Dictionary<Guid, RunOutcome> _outcomes = new();

    private static readonly ActivityOptions NotifyOpts = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(5),
        RetryPolicy = new() { MaximumAttempts = 3 },
    };

    [WorkflowRun]
    public async Task RunAsync(CoordinatorInit init)
    {
        while (true)
        {
            // Sleep until there is something to do (durable wait)
            await Workflow.WaitConditionAsync(() =>
                _status != AgentStatus.Paused &&
                _triggersQueue.Count > 0);

            if (_status == AgentStatus.Paused)
                continue;

            var trigger = _triggersQueue.Dequeue();

            _status = AgentStatus.Running;
            _runNumber++;

            var runInput = new RunInput(init.AgentId, trigger);

            _currentRunId = $"run-{trigger.Source.ToString().ToLowerInvariant()}-{trigger.TriggerId:N}-num-{_runNumber}";

            RunOutcome? outcome;
            try
            {
                outcome = await Workflow.ExecuteChildWorkflowAsync(
                    (AgentRunWorkflow wf) => wf.RunAsync(runInput),
                    new ChildWorkflowOptions
                    {
                        Id = _currentRunId,
                        TaskQueue = "aoc-agent-task-queue",
                    });
            }
            catch (Exception e)
            {
                ActivityExecutionContext.Current.Logger.LogError("Run failed for trigger {TriggerId} with error: {Error}", trigger.TriggerId, e.ToString());
                var isCanceledException = TemporalException.IsCanceledException(e);
                outcome = isCanceledException
                    ? new RunOutcome(RunOutcomeKind.Canceled, null, e.Message)
                    : new RunOutcome(RunOutcomeKind.Failed, null, e.Message);
            }

            _outcomes[trigger.TriggerId] = outcome;

            if (outcome.Kind == RunOutcomeKind.Completed)
            {
                _status = AgentStatus.Idle;

                if (outcome.AgentReply is not null)
                {
                    try
                    {
                        await Workflow.ExecuteActivityAsync(
                            (McpActivities a) => a.NotifyUserAsync(init.AgentId, outcome.AgentReply.Text),
                            NotifyOpts);
                    }
                    catch
                    {
                        // Don't fail the workflow if notification fails, just log it
                        ActivityExecutionContext.Current.Logger.LogError("Failed to notify user for trigger {TriggerId}", trigger.TriggerId);
                    }
                }
            }

            _currentRunId = null;

            // Keep history bounded
            if (Workflow.AllHandlersFinished &&
                (Workflow.ContinueAsNewSuggested || Workflow.CurrentHistoryLength > 20_000))
            {
                throw Workflow.CreateContinueAsNewException(
                    (AgentCoordinatorWorkflow wf) => wf.RunAsync(init));
            }
        }
    }

    // Fire-and-forget enqueue (cron trigger will use this)
    [WorkflowSignal]
    public Task EnqueueAsync(TriggerEvent trigger)
    {
        EnqueueInternal(trigger);
        return Task.CompletedTask;
    }

    // Update: enqueue and wait for a result (simple UX)
    [WorkflowUpdate]
    public async Task<RunOutcome> AskAsync(TriggerEvent trigger)
    {
        EnqueueInternal(trigger);
        await Workflow.WaitConditionAsync(() => _outcomes.ContainsKey(trigger.TriggerId));
        return _outcomes[trigger.TriggerId];
    }

    [WorkflowUpdate]
    public Task PauseAsync()
    {
        _status = AgentStatus.Paused;
        return Task.CompletedTask;
    }

    [WorkflowUpdate]
    public Task ResumeAsync()
    {
        if (_status == AgentStatus.Paused)
            _status = AgentStatus.Idle;
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public AgentStatusDto GetStatus() =>
        new(_status, _currentRunId, _triggersQueue.Count, _runNumber);

    private void EnqueueInternal(TriggerEvent trigger)
    {
        if (_triggerIds.Contains(trigger.TriggerId))
            return;

        _triggerIds.Add(trigger.TriggerId);
        _triggersQueue.Enqueue(trigger);
    }
}
