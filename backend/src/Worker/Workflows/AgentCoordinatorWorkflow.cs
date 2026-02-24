using Temporalio.Workflows;
using Worker.Activities;
using Worker.Extensions;
using Worker.Models;

namespace Worker.Workflows;

[Workflow]
public class AgentCoordinatorWorkflow
{
    private readonly Queue<TriggerEvent> _triggersQueue = new();
    private readonly HashSet<string> _triggerIds = new();

    private AgentStatus _status = AgentStatus.Idle;
    private PendingQuestion? _pendingQuestion;
    private string? _currentRunId;
    private int _runNumber;

    private readonly Dictionary<string, RunOutcome> _outcomes = new();

    private static readonly ActivityOptions NotifyOpts = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(1),
        RetryPolicy = new() { MaximumAttempts = 5 }
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

            // If we are waiting for user, ignore unrelated triggers (except matching answer)
            if (_pendingQuestion is not null)
            {
                if (trigger.Source != TriggerSource.UserAnswer ||
                    trigger.AnswerToQuestionId != _pendingQuestion.QuestionId)
                {
                    // Put it back at end (or drop?)
                    _triggersQueue.Enqueue(trigger);
                    await Workflow.WaitConditionAsync(() => _triggersQueue.Any(t =>
                        t.Source == TriggerSource.UserAnswer && t.AnswerToQuestionId == _pendingQuestion.QuestionId));
                    continue;
                }
            }

            _status = AgentStatus.Running;
            _runNumber++;

            var runInput = new RunInput(init.AgentId, trigger, _pendingQuestion);

            _currentRunId = $"run-{Workflow.UtcNow.ToUnixTimeMilliseconds()}";

            var outcome = await Workflow.ExecuteChildWorkflowAsync(
                (AgentRunWorkflow wf) => wf.RunAsync(runInput),
                new ChildWorkflowOptions
                {
                    TaskQueue = "aoc-agent-task-queue"
                });

            _outcomes[trigger.TriggerId] = outcome;

            if (outcome.Kind == RunOutcomeKind.BlockedOnUser && outcome.NewPendingQuestion is not null)
            {
                _pendingQuestion = outcome.NewPendingQuestion;
                _status = AgentStatus.WaitingForUser;

                await Workflow.ExecuteActivityAsync(
                    (AgentActivities a) => a.NotifyUserAsync(init.AgentId, $"Question: {_pendingQuestion.Text}"),
                    NotifyOpts);
            }
            else if (outcome.Kind == RunOutcomeKind.Completed && outcome.AgentReply is not null)
            {
                _pendingQuestion = null;
                _status = AgentStatus.Idle;

                await Workflow.ExecuteActivityAsync(
                    (AgentActivities a) => a.NotifyUserAsync(init.AgentId, outcome.AgentReply),
                    NotifyOpts);
            }
            else
            {
                _status = _pendingQuestion is null ? AgentStatus.Idle : AgentStatus.WaitingForUser;
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
            _status = _pendingQuestion is null ? AgentStatus.Idle : AgentStatus.WaitingForUser;
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public AgentStatusDto GetStatus() =>
        new(_status, _currentRunId, _pendingQuestion, _triggersQueue.Count, _runNumber);

    [WorkflowQuery]
    public PendingQuestion? GetPendingQuestion() => _pendingQuestion;

    private void EnqueueInternal(TriggerEvent trigger)
    {
        if (_triggerIds.Contains(trigger.TriggerId))
            return;

        _triggerIds.Add(trigger.TriggerId);
        _triggersQueue.Enqueue(trigger);
    }
}
