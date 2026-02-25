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

            var trigger = _triggersQueue.Dequeue();

            _status = AgentStatus.Running;
            _runNumber++;
            _currentRunId = $"run-{trigger.Source.ToString().ToLowerInvariant()}-{trigger.TriggerId:N}-num-{_runNumber}";
            _error = null;
            await InsertRunStartMessage();

            var runInput = new RunInput(_currentRunId, _agentId, trigger);

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
                    ? new RunOutcome(RunOutcomeKind.Canceled, e.Message ?? "Run was canceled.")
                    : new RunOutcome(RunOutcomeKind.Failed, e.Message ?? "An error occurred during agent execution.");
                await InsertRunErrorMessage(outcome.Error!);
            }

            _outcomes[trigger.TriggerId] = outcome;
            _currentRunId = null;
            _status = outcome.Kind == RunOutcomeKind.Failed ? AgentStatus.Failed : AgentStatus.Idle;
            _error = outcome.Error;
            await InsertRunFinishedMessage(outcome);

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
            RunId = _currentRunId!,
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentJson = JsonSerializer.Serialize(AocAiContentDto.FromAocAiContent(new AocRunStart { RunId = _currentRunId!, ThreadId = _agentId.ToString() })),
        };
        // ToDo: Do not silently swallow the exception here
        await ResultWrapper.Wrap(() => Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(startTaskMessage), Options));
    }

    private async Task InsertRunFinishedMessage(RunOutcome outcome)
    {
        var endTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-finished-{_currentRunId}"),
            AgentId = _agentId,
            RunId = _currentRunId!,
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentJson = JsonSerializer.Serialize(AocAiContentDto.FromAocAiContent(new AocRunFinished { RunId = _currentRunId!, ThreadId = _agentId.ToString(), Result = JsonElement.Parse(JsonSerializer.Serialize(outcome)) })),
        };
        // ToDo: Do not silently swallow the exception here
        await ResultWrapper.Wrap(() => Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(endTaskMessage), Options));
    }

    private async Task InsertRunErrorMessage(string error)
    {
        var errorTaskMessage = new LlmChatMessage
        {
            Id = HashUtils.HashStringToGuid($"run-error-{_currentRunId}"),
            AgentId = _agentId,
            RunId = _currentRunId!,
            IsHidden = true,
            Role = ChatRole.System,
            CreatedAt = Workflow.UtcNow,
            ContentJson = JsonSerializer.Serialize(AocAiContentDto.FromAocAiContent(new AocRunError { Message = error })),
        };
        // ToDo: Do not silently swallow the exception here
        await ResultWrapper.Wrap(() => Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(errorTaskMessage), Options));
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
