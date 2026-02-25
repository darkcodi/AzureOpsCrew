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
    private readonly Queue<TriggerEvent> _triggersQueue = new();
    private readonly HashSet<Guid> _triggerIds = new();
    private AgentStatus _status = AgentStatus.Idle;
    private Guid _agentId = Guid.Empty;
    private string? _currentRunId;
    private TriggerEvent? _currentTrigger;
    private long _runNumber;
    private string? _error;
    private readonly Dictionary<Guid, RunOutcome> _outcomes = new();

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

            _currentTrigger = _triggersQueue.Dequeue();

            _status = AgentStatus.Running;
            _runNumber++;
            _error = null;

            _currentRunId = $"run-{_currentTrigger.Source.ToString().ToLowerInvariant()}-{_currentTrigger.TriggerId:N}-num-{_runNumber}";

            await InsertRunStartMessage();
            await InsertRunTriggerMessage();

            var runInput = new RunInput(_currentRunId, _agentId, _currentTrigger);

            RunOutcome? outcome;
            bool hasErrored = false;
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
                ActivityExecutionContext.Current.Logger.LogError("Run failed for trigger {TriggerId} with error: {Error}", _currentTrigger.TriggerId, e.ToString());
                var isCanceledException = TemporalException.IsCanceledException(e);
                outcome = isCanceledException
                    ? new RunOutcome(RunOutcomeKind.Canceled, e.Message ?? "Run was canceled.")
                    : new RunOutcome(RunOutcomeKind.Failed, e.Message ?? "An error occurred during agent execution.");
                await InsertRunErrorMessage(outcome.Error!);
                hasErrored = true;
            }

            _outcomes[_currentTrigger.TriggerId] = outcome;
            _status = outcome.Kind == RunOutcomeKind.Failed ? AgentStatus.Failed : AgentStatus.Idle;
            _error = outcome.Error;

            // Only send RUN_FINISHED if we haven't already sent RUN_ERROR
            // The @ag-ui/core library rejects any events after RUN_ERROR
            if (!hasErrored)
            {
                await InsertRunFinishedMessage(outcome);
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

    private async Task InsertRunStartMessage()
    {
        var startTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-start-{_currentRunId}"),
            AgentId = _agentId,
            RunId = _currentRunId ?? throw new InvalidOperationException("CurrentRunId cannot be null when inserting run start message."),
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentType = LlmMessageContentType.RunStart,
            ContentJson = JsonSerializer.Serialize(new AocRunStart { RunId = _currentRunId!, ThreadId = _agentId.ToString() }),
        };
        await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(startTaskMessage), Options);
    }

    private async Task InsertRunTriggerMessage()
    {
        var triggerMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"trigger-message-{_currentRunId!}-{_currentTrigger!.TriggerId}"),
            AgentId = _agentId,
            RunId = _currentRunId ?? throw new InvalidOperationException("CurrentRunId cannot be null when inserting run start message."),
            IsHidden = false,
            Role = _currentTrigger.Source == TriggerSource.Cron ? ChatRole.System : ChatRole.User,
            AuthorName = _currentTrigger.Source == TriggerSource.Cron ? "SYSTEM" : "User",
            CreatedAt = Workflow.UtcNow,
            ContentType = LlmMessageContentType.TextContent,
            ContentJson = JsonSerializer.Serialize(new AocTextContent { Text = _currentTrigger.Text ?? "" }),
        };
        await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(triggerMessage), Options);
    }

    private async Task InsertRunFinishedMessage(RunOutcome outcome)
    {
        var endTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-finished-{_currentRunId}"),
            AgentId = _agentId,
            RunId = _currentRunId ?? throw new InvalidOperationException("CurrentRunId cannot be null when inserting run start message."),
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentType = LlmMessageContentType.RunFinished,
            ContentJson = JsonSerializer.Serialize(new AocRunFinished { RunId = _currentRunId!, ThreadId = _agentId.ToString(), Result = JsonElement.Parse(JsonSerializer.Serialize(outcome)) }),
        };
        await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(endTaskMessage), Options);
    }

    private async Task InsertRunErrorMessage(string error)
    {
        var errorTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-error-{_currentRunId}"),
            AgentId = _agentId,
            RunId = _currentRunId ?? throw new InvalidOperationException("CurrentRunId cannot be null when inserting run start message."),
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
        new(_status, _currentRunId, _triggersQueue.Count, _runNumber, _error);

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
        if (_triggerIds.Contains(trigger.TriggerId))
            return;

        _triggerIds.Add(trigger.TriggerId);
        _triggersQueue.Enqueue(trigger);
    }
}
