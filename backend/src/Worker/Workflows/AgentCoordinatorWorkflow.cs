using System.Text.Json;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Utils;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Temporalio.Client;
using Temporalio.Exceptions;
using Temporalio.Workflows;
using Worker.Activities;
using Worker.Models;
using Worker.Models.Content;

namespace Worker.Workflows;

[Workflow]
public class AgentCoordinatorWorkflow
{
    // agent/thread this workflow is managing - set at initialization and used for all runs
    private Guid _agentId = Guid.Empty;

    // manage the queue of triggers and their outcomes in-memory within the workflow
    private readonly Queue<TriggerEvent> _triggersQueue = new();
    private readonly HashSet<Guid> _queuedTriggerIds = new();
    private readonly Dictionary<Guid, RunOutcome> _outcomes = new();

    // current state of the agent
    private TriggerEvent? _trigger;
    private long _runCounter;
    private AgentStatus _status = AgentStatus.Idle;
    private string? _error;

    // computed properties for current run
    private Guid? TriggerId => _trigger?.TriggerId;
    private Guid? ThreadId => _trigger?.ThreadId;
    private Guid? RunId => _trigger?.RunId;

    private static readonly ActivityOptions Options = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new() { MaximumAttempts = 3 }
    };

    [WorkflowRun]
    public async Task RunAsync(CoordinatorInit init)
    {
        _agentId = init.AgentId;

        while (true)
        {
            // Sleep until there is something to do (durable wait)
            await Workflow.WaitConditionAsync(() =>
                _status != AgentStatus.Paused &&
                _status != AgentStatus.Failed &&
                _triggersQueue.Count > 0);

            if (_status == AgentStatus.Paused || _status == AgentStatus.Failed)
                continue;

            _trigger = _triggersQueue.Dequeue();
            _runCounter++;
            _status = AgentStatus.Running;
            _error = null;

            await InsertRunStartMessage(_agentId, ThreadId!.Value, RunId!.Value);
            await InsertRunTriggerMessage(_agentId, _trigger);

            var runInput = new RunInput(RunId!.Value, _agentId, _trigger);

            RunOutcome? outcome;
            bool hasErrored = false;
            try
            {
                outcome = await Workflow.ExecuteChildWorkflowAsync(
                    (AgentRunWorkflow wf) => wf.RunAsync(runInput),
                    new ChildWorkflowOptions
                    {
                        Id = $"run-{TriggerId:N}-num-{_runCounter}",
                        TaskQueue = "aoc-agent-task-queue",
                    });
            }
            catch (Exception e)
            {
                ActivityExecutionContext.Current.Logger.LogError("Run failed for trigger {TriggerId} with error: {Error}", _trigger.TriggerId, e.ToString());
                var isCanceledException = TemporalException.IsCanceledException(e);
                outcome = isCanceledException
                    ? new RunOutcome(RunOutcomeKind.Canceled, e.Message ?? "Run was canceled.")
                    : new RunOutcome(RunOutcomeKind.Failed, e.Message ?? "An error occurred during agent execution.");
                await InsertRunErrorMessage(_agentId, ThreadId!.Value, RunId!.Value, outcome.Error!);
                hasErrored = true;
            }

            _outcomes[_trigger.TriggerId] = outcome;
            _status = outcome.Kind == RunOutcomeKind.Failed ? AgentStatus.Failed : AgentStatus.Idle;
            _error = outcome.Error;

            // Only send RUN_FINISHED if we haven't already sent RUN_ERROR
            // The @ag-ui/core library rejects any events after RUN_ERROR
            if (!hasErrored)
            {
                await InsertRunFinishedMessage(_agentId, ThreadId!.Value, RunId!.Value);
            }

            _trigger = null;

            // Keep history bounded
            if (Workflow.AllHandlersFinished &&
                (Workflow.ContinueAsNewSuggested || Workflow.CurrentHistoryLength > 20_000))
            {
                throw Workflow.CreateContinueAsNewException(
                    (AgentCoordinatorWorkflow wf) => wf.RunAsync(init));
            }
        }
    }

    private static async Task InsertRunStartMessage(Guid agentId, Guid threadId, Guid runId)
    {
        var startTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-start-{runId}"),
            AgentId = agentId,
            RunId = runId,
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentType = LlmMessageContentType.RunStart,
            ContentJson = JsonSerializer.Serialize(new AocRunStart { RunId = runId, ThreadId = threadId }),
        };
        await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(startTaskMessage), Options);
    }

    private static async Task InsertRunTriggerMessage(Guid agentId, TriggerEvent trigger)
    {
        var triggerMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"trigger-message-{trigger.RunId}"),
            AgentId = agentId,
            RunId = trigger.RunId,
            IsHidden = false,
            Role = trigger.Source == TriggerSource.Cron ? ChatRole.System : ChatRole.User,
            AuthorName = trigger.Source == TriggerSource.Cron ? "SYSTEM" : "User",
            CreatedAt = Workflow.UtcNow,
            ContentType = LlmMessageContentType.TextContent,
            ContentJson = JsonSerializer.Serialize(new AocTextContent { Text = trigger.Text ?? "" }),
        };
        await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(triggerMessage), Options);
    }

    private static async Task InsertRunFinishedMessage(Guid agentId, Guid threadId, Guid runId)
    {
        var endTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-finished-{runId}"),
            AgentId = agentId,
            RunId = runId,
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentType = LlmMessageContentType.RunFinished,
            ContentJson = JsonSerializer.Serialize(new AocRunFinished { RunId = runId, ThreadId = threadId, Result = null }),
        };
        await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(endTaskMessage), Options);
    }

    private static async Task InsertRunErrorMessage(Guid agentId, Guid threadId, Guid runId, string error)
    {
        var errorTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-error-{runId}"),
            AgentId = agentId,
            RunId = runId,
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentType = LlmMessageContentType.RunError,
            ContentJson = JsonSerializer.Serialize(new AocRunError { Message = error }),
        };
        await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(errorTaskMessage), Options);
    }

    // Fire-and-forget enqueue (cron trigger will use this)
    [WorkflowSignal]
    public Task EnqueueAsync(TriggerEvent trigger)
    {
        EnqueueInternal(trigger);
        return Task.CompletedTask;
    }

    // Update: enqueue and wait for a result
    [WorkflowUpdate]
    public async Task<RunOutcome> ExecuteAsync(TriggerEvent trigger)
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
        if (_status == AgentStatus.Paused || _status == AgentStatus.Failed)
            _status = AgentStatus.Idle;
        return Task.CompletedTask;
    }

    [WorkflowQuery]
    public AgentStatusDto GetStatus() =>
        new(_status, RunId, _triggersQueue.Count, _runCounter, _error);

    public static string CoordinatorWorkflowId(Guid agentId) => $"agent:{agentId}";

    public static async Task EnsureCoordinatorStartedAsync(TemporalClient client, Guid agentId)
    {
        var coordinationWorkflowId = CoordinatorWorkflowId(agentId);

        try
        {
            await client.StartWorkflowAsync(
                (AgentCoordinatorWorkflow wf) => wf.RunAsync(new CoordinatorInit(agentId)),
                new(id: coordinationWorkflowId, taskQueue: "aoc-agent-task-queue"));
        }
        catch (WorkflowAlreadyStartedException)
        {
            // fine (desired) outcome - workflow is already running
        }
    }

    private void EnqueueInternal(TriggerEvent trigger)
    {
        if (_queuedTriggerIds.Contains(trigger.TriggerId))
            return;

        _queuedTriggerIds.Add(trigger.TriggerId);
        _triggersQueue.Enqueue(trigger);
    }
}
