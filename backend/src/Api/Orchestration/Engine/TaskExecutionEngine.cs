using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Execution;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;

namespace AzureOpsCrew.Api.Orchestration.Engine;

/// <summary>
/// Core Task Execution Engine. Manages run lifecycle:
/// create run → plan → decompose → select next task → execute → observe → decide → loop.
/// Persists every state change to DB. Chat is the UI; engine is the backend.
/// </summary>
public class TaskExecutionEngine
{
    private readonly AzureOpsCrewContext _db;
    private readonly ExecutionEngineSettings _settings;
    private readonly TaskPlanner _planner;
    private readonly TaskExecutor _executor;

    public TaskExecutionEngine(
        AzureOpsCrewContext db,
        IOptions<ExecutionEngineSettings> settings,
        TaskPlanner planner,
        TaskExecutor executor)
    {
        _db = db;
        _settings = settings.Value;
        _planner = planner;
        _executor = executor;
    }

    /// <summary>
    /// Create a new execution run from a user request.
    /// Returns the created run with root task.
    /// </summary>
    public async Task<ExecutionRun> CreateRunAsync(
        Guid channelId, int userId, string threadId, string userRequest,
        CancellationToken ct = default)
    {
        var run = ExecutionRun.Create(channelId, userId, threadId, userRequest);
        run.Status = ExecutionRunStatus.Planning;
        run.StartedAt = DateTime.UtcNow;

        // Create root task
        var rootTask = ExecutionTask.Create(run.Id, userRequest, ExecutionTaskType.RootGoal);
        rootTask.Status = ExecutionTaskStatus.Running;
        rootTask.Goal = userRequest;
        rootTask.StartedAt = DateTime.UtcNow;

        _db.ExecutionRuns.Add(run);
        _db.ExecutionTasks.Add(rootTask);

        // Journal entry
        _db.JournalEntries.Add(JournalEntry.Create(
            run.Id, JournalEntryType.Info,
            $"Run created for: {Truncate(userRequest, 200)}"));

        await _db.SaveChangesAsync(ct);
        Log.Information("[Engine] Run {RunId} created with root task {TaskId}", run.Id, rootTask.Id);
        return run;
    }

    /// <summary>
    /// Resume an existing run (e.g., after approval or interruption).
    /// </summary>
    public async Task<ExecutionRun?> ResumeRunAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _db.ExecutionRuns
            .Include(r => r.Tasks)
            .Include(r => r.ApprovalRequests)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

        if (run is null) return null;

        // Check if any pending approval was resolved
        var pendingApproval = run.ApprovalRequests
            .FirstOrDefault(a => a.Status == ApprovalStatus.Pending);

        if (pendingApproval is not null)
        {
            Log.Information("[Engine] Run {RunId} still has pending approval {ApprovalId}", runId, pendingApproval.Id);
            return run; // Still waiting
        }

        // If run was waiting for approval and all approvals resolved, transition back to running
        if (run.Status == ExecutionRunStatus.WaitingForApproval)
        {
            run.Status = ExecutionRunStatus.Running;
            run.UpdatedAt = DateTime.UtcNow;

            // Resume the task that was waiting
            var waitingTask = run.Tasks.FirstOrDefault(t => t.Status == ExecutionTaskStatus.WaitingForApproval);
            if (waitingTask is not null)
            {
                waitingTask.Status = ExecutionTaskStatus.Ready;
                waitingTask.UpdatedAt = DateTime.UtcNow;
            }

            _db.JournalEntries.Add(JournalEntry.Create(
                run.Id, JournalEntryType.Info, "Run resumed after approval"));

            await _db.SaveChangesAsync(ct);
        }

        return run;
    }

    /// <summary>
    /// Execute the main engine loop: plan → select next task → execute → observe → decide.
    /// Returns when run completes, hits a checkpoint, or exhausts budget.
    /// </summary>
    public async Task<EngineStepResult> RunStepAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await LoadRunAsync(runId, ct);
        if (run is null)
            return EngineStepResult.Error("Run not found");

        // Check terminal states
        if (IsTerminal(run.Status))
            return EngineStepResult.Completed(run, "Run already in terminal state");

        // Check budget
        var budgetCheck = CheckBudget(run);
        if (budgetCheck is not null)
            return budgetCheck;

        // Check timeout
        if (run.StartedAt.HasValue &&
            (DateTime.UtcNow - run.StartedAt.Value).TotalMinutes > _settings.RunTimeoutMinutes)
        {
            return await TerminateRunAsync(run, ExecutionRunStatus.TimedOut,
                $"Run exceeded timeout of {_settings.RunTimeoutMinutes} minutes", ct);
        }

        try
        {
            // Phase 1: Planning (if needed)
            if (run.Status == ExecutionRunStatus.Planning || run.CurrentPlan is null)
            {
                var planResult = await _planner.CreatePlanAsync(run, ct);
                if (!planResult.Success)
                    return EngineStepResult.Error($"Planning failed: {planResult.Error}");

                run.Status = ExecutionRunStatus.Running;
                run.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);
            }

            // Phase 2: Select next runnable task
            var nextTask = SelectNextTask(run);
            if (nextTask is null)
            {
                // Check if all tasks completed
                var allTasks = run.Tasks.Where(t => t.TaskType != ExecutionTaskType.RootGoal).ToList();
                if (allTasks.All(t => t.Status is ExecutionTaskStatus.Succeeded or ExecutionTaskStatus.Skipped or ExecutionTaskStatus.Cancelled))
                {
                    return await CompleteRunAsync(run, ct);
                }

                // Check if any tasks failed
                if (allTasks.Any(t => t.Status == ExecutionTaskStatus.Failed))
                {
                    // Check if replanning is possible
                    if (run.TotalReplans < _settings.MaxReplans)
                    {
                        return await TriggerReplanAsync(run, "Some tasks failed, replanning", ct);
                    }
                    return await TerminateRunAsync(run, ExecutionRunStatus.Failed,
                        "Tasks failed and max replans reached", ct);
                }

                // Tasks still blocked/waiting
                run.ConsecutiveNonProgressSteps++;
                run.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(ct);

                return EngineStepResult.Waiting(run, "No runnable tasks, waiting for dependencies/approvals");
            }

            // Phase 3: Execute the task
            nextTask.Status = ExecutionTaskStatus.Running;
            nextTask.StartedAt ??= DateTime.UtcNow;
            nextTask.UpdatedAt = DateTime.UtcNow;
            run.TotalSteps++;
            await _db.SaveChangesAsync(ct);

            _db.JournalEntries.Add(JournalEntry.Create(
                run.Id, JournalEntryType.TaskStarted,
                $"Starting task: {nextTask.Title}",
                nextTask.AssignedAgent, nextTask.Id));
            await _db.SaveChangesAsync(ct);

            var execResult = await _executor.ExecuteTaskAsync(run, nextTask, ct);

            // Phase 4: Observe and decide
            nextTask.StepCount++;
            run.UpdatedAt = DateTime.UtcNow;

            if (execResult.NeedsApproval)
            {
                return await PauseForApprovalAsync(run, nextTask, execResult, ct);
            }

            if (execResult.NeedsReplan)
            {
                return await TriggerReplanAsync(run, execResult.ReplanReason ?? "Executor requested replan", ct);
            }

            if (execResult.Success)
            {
                nextTask.Status = ExecutionTaskStatus.Succeeded;
                nextTask.CompletedAt = DateTime.UtcNow;
                nextTask.ResultSummary = execResult.Summary;
                run.ConsecutiveNonProgressSteps = 0; // Progress made

                _db.JournalEntries.Add(JournalEntry.Create(
                    run.Id, JournalEntryType.TaskCompleted,
                    $"Task completed: {nextTask.Title}",
                    nextTask.AssignedAgent, nextTask.Id,
                    execResult.Summary));
            }
            else
            {
                nextTask.RetryCount++;
                if (nextTask.RetryCount > _settings.MaxRetriesPerTask)
                {
                    nextTask.Status = ExecutionTaskStatus.Failed;
                    nextTask.CompletedAt = DateTime.UtcNow;
                    nextTask.ResultSummary = $"Failed after {nextTask.RetryCount} retries: {execResult.Error}";

                    _db.JournalEntries.Add(JournalEntry.Create(
                        run.Id, JournalEntryType.TaskFailed,
                        $"Task failed: {nextTask.Title} — {execResult.Error}",
                        nextTask.AssignedAgent, nextTask.Id));
                }
                else
                {
                    nextTask.Status = ExecutionTaskStatus.Ready; // Retry
                    _db.JournalEntries.Add(JournalEntry.Create(
                        run.Id, JournalEntryType.Info,
                        $"Task will retry ({nextTask.RetryCount}/{_settings.MaxRetriesPerTask}): {nextTask.Title}",
                        nextTask.AssignedAgent, nextTask.Id));
                }
            }

            nextTask.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Unblock dependent tasks
            await UpdateDependencyStatusesAsync(run, ct);

            return EngineStepResult.Continue(run, nextTask,
                $"Task '{nextTask.Title}' → {nextTask.Status}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Engine] Error in run step for {RunId}", runId);
            _db.JournalEntries.Add(JournalEntry.Create(
                run.Id, JournalEntryType.Error,
                $"Engine error: {ex.Message}"));
            await _db.SaveChangesAsync(ct);

            return EngineStepResult.Error($"Engine error: {ex.Message}");
        }
    }

    /// <summary>
    /// Run the full execution loop until completion, checkpoint, or budget.
    /// </summary>
    public async Task<EngineStepResult> RunToCompletionAsync(Guid runId, CancellationToken ct = default)
    {
        EngineStepResult lastResult;
        int safetyLimit = _settings.MaxStepsPerRun + 10; // Absolute safety

        do
        {
            lastResult = await RunStepAsync(runId, ct);
            safetyLimit--;

            if (safetyLimit <= 0)
            {
                var run = await LoadRunAsync(runId, ct);
                if (run is not null)
                    return await TerminateRunAsync(run, ExecutionRunStatus.BudgetExhausted,
                        "Safety limit reached in execution loop", ct);
                return EngineStepResult.Error("Safety limit reached");
            }

        } while (lastResult.ShouldContinue);

        return lastResult;
    }

    /// <summary>
    /// Handle approval response from user.
    /// </summary>
    public async Task<EngineStepResult> HandleApprovalAsync(
        Guid runId, Guid approvalId, bool approved, string? reason = null,
        CancellationToken ct = default)
    {
        var approval = await _db.ApprovalRequests.FirstOrDefaultAsync(a => a.Id == approvalId, ct);
        if (approval is null)
            return EngineStepResult.Error("Approval request not found");

        approval.Status = approved ? ApprovalStatus.Approved : ApprovalStatus.Denied;
        approval.DecisionReason = reason;
        approval.RespondedAt = DateTime.UtcNow;
        approval.RespondedBy = "user";

        _db.JournalEntries.Add(JournalEntry.Create(
            runId,
            approved ? JournalEntryType.ApprovalGranted : JournalEntryType.ApprovalDenied,
            $"Approval {(approved ? "granted" : "denied")} for: {approval.ProposedAction}",
            detail: reason));

        if (!approved && approval.TaskId.HasValue)
        {
            // Cancel the task that needed approval
            var task = await _db.ExecutionTasks.FirstOrDefaultAsync(t => t.Id == approval.TaskId, ct);
            if (task is not null)
            {
                task.Status = ExecutionTaskStatus.Cancelled;
                task.UpdatedAt = DateTime.UtcNow;
                task.ResultSummary = $"Cancelled: approval denied — {reason}";
            }
        }

        await _db.SaveChangesAsync(ct);

        // Resume the run
        await ResumeRunAsync(runId, ct);
        return EngineStepResult.Continue(null, null, $"Approval {(approved ? "granted" : "denied")}");
    }

    // ─── Private helpers ───

    private async Task<ExecutionRun?> LoadRunAsync(Guid runId, CancellationToken ct)
    {
        return await _db.ExecutionRuns
            .Include(r => r.Tasks)
            .Include(r => r.Artifacts)
            .Include(r => r.ApprovalRequests)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);
    }

    private ExecutionTask? SelectNextTask(ExecutionRun run)
    {
        var tasks = run.Tasks
            .Where(t => t.TaskType != ExecutionTaskType.RootGoal)
            .ToList();

        // Find tasks that are ready (status = Ready, all dependencies satisfied)
        var readyTasks = tasks
            .Where(t => t.Status == ExecutionTaskStatus.Ready)
            .Where(t => AreDependenciesSatisfied(t, tasks))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.CreatedAt)
            .ToList();

        return readyTasks.FirstOrDefault();
    }

    private static bool AreDependenciesSatisfied(ExecutionTask task, List<ExecutionTask> allTasks)
    {
        var depIds = task.GetDependencyIds();
        if (depIds.Count == 0) return true;

        return depIds.All(depId =>
        {
            var dep = allTasks.FirstOrDefault(t => t.Id == depId);
            return dep?.Status is ExecutionTaskStatus.Succeeded or ExecutionTaskStatus.Skipped;
        });
    }

    private async Task UpdateDependencyStatusesAsync(ExecutionRun run, CancellationToken ct)
    {
        var tasks = run.Tasks.Where(t => t.Status == ExecutionTaskStatus.Blocked).ToList();
        var allTasks = run.Tasks.ToList();
        var changed = false;

        foreach (var task in tasks)
        {
            if (AreDependenciesSatisfied(task, allTasks))
            {
                task.Status = ExecutionTaskStatus.Ready;
                task.UpdatedAt = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
            await _db.SaveChangesAsync(ct);
    }

    private EngineStepResult? CheckBudget(ExecutionRun run)
    {
        if (run.TotalSteps >= _settings.MaxStepsPerRun)
        {
            _ = TerminateRunAsync(run, ExecutionRunStatus.BudgetExhausted,
                $"Max steps ({_settings.MaxStepsPerRun}) exhausted").GetAwaiter().GetResult();
            return EngineStepResult.BudgetExhausted(run,
                $"Reached max steps limit ({_settings.MaxStepsPerRun})");
        }

        if (run.ConsecutiveNonProgressSteps >= _settings.MaxConsecutiveNonProgressSteps)
        {
            _ = TerminateRunAsync(run, ExecutionRunStatus.BudgetExhausted,
                $"Max non-progress steps ({_settings.MaxConsecutiveNonProgressSteps}) exhausted").GetAwaiter().GetResult();
            return EngineStepResult.BudgetExhausted(run,
                $"No progress for {_settings.MaxConsecutiveNonProgressSteps} consecutive steps");
        }

        return null;
    }

    private async Task<EngineStepResult> PauseForApprovalAsync(
        ExecutionRun run, ExecutionTask task, TaskExecutionResult execResult, CancellationToken ct)
    {
        var approval = ApprovalRequest.Create(
            run.Id,
            execResult.ApprovalActionType ?? "unknown",
            execResult.ApprovalProposedAction ?? execResult.Summary ?? "",
            execResult.ApprovalRiskLevel,
            task.Id);
        approval.RollbackPlan = execResult.ApprovalRollbackPlan;
        approval.VerificationPlan = execResult.ApprovalVerificationPlan;
        approval.AffectedResources = execResult.ApprovalAffectedResources;

        _db.ApprovalRequests.Add(approval);

        task.Status = ExecutionTaskStatus.WaitingForApproval;
        task.UpdatedAt = DateTime.UtcNow;
        run.Status = ExecutionRunStatus.WaitingForApproval;
        run.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.Add(JournalEntry.Create(
            run.Id, JournalEntryType.ApprovalRequested,
            $"Approval required: {approval.ProposedAction}",
            task.AssignedAgent, task.Id));

        await _db.SaveChangesAsync(ct);

        Log.Information("[Engine] Run {RunId} paused for approval {ApprovalId}", run.Id, approval.Id);
        return EngineStepResult.WaitingForApproval(run, approval);
    }

    private async Task<EngineStepResult> TriggerReplanAsync(
        ExecutionRun run, string reason, CancellationToken ct)
    {
        run.TotalReplans++;

        if (run.TotalReplans > _settings.MaxReplans)
        {
            return await TerminateRunAsync(run, ExecutionRunStatus.BudgetExhausted,
                $"Max replans ({_settings.MaxReplans}) reached", ct);
        }

        run.Status = ExecutionRunStatus.Replanning;
        run.LastReplanReason = reason;
        run.UpdatedAt = DateTime.UtcNow;

        _db.JournalEntries.Add(JournalEntry.Create(
            run.Id, JournalEntryType.ReplanTriggered,
            $"Replanning (#{run.TotalReplans}): {reason}"));

        await _db.SaveChangesAsync(ct);

        // Execute replanning
        var replanResult = await _planner.ReplanAsync(run, reason, ct);
        if (!replanResult.Success)
        {
            return await TerminateRunAsync(run, ExecutionRunStatus.Failed,
                $"Replanning failed: {replanResult.Error}", ct);
        }

        run.Status = ExecutionRunStatus.Running;
        run.PlanRevision++;
        run.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return EngineStepResult.Continue(run, null, $"Replanned (revision {run.PlanRevision}): {reason}");
    }

    private async Task<EngineStepResult> CompleteRunAsync(ExecutionRun run, CancellationToken ct)
    {
        // Generate summary from completed tasks
        var completedTasks = run.Tasks
            .Where(t => t.TaskType != ExecutionTaskType.RootGoal && t.Status == ExecutionTaskStatus.Succeeded)
            .ToList();

        var summaryParts = completedTasks
            .Select(t => $"- {t.Title}: {t.ResultSummary ?? "completed"}")
            .ToList();

        run.Status = ExecutionRunStatus.Succeeded;
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        run.ResultSummary = string.Join("\n", summaryParts);

        // Complete root task
        var rootTask = run.Tasks.FirstOrDefault(t => t.TaskType == ExecutionTaskType.RootGoal);
        if (rootTask is not null)
        {
            rootTask.Status = ExecutionTaskStatus.Succeeded;
            rootTask.CompletedAt = DateTime.UtcNow;
            rootTask.UpdatedAt = DateTime.UtcNow;
            rootTask.ResultSummary = run.ResultSummary;
        }

        _db.JournalEntries.Add(JournalEntry.Create(
            run.Id, JournalEntryType.Info,
            $"Run completed successfully with {completedTasks.Count} tasks"));

        await _db.SaveChangesAsync(ct);
        Log.Information("[Engine] Run {RunId} completed successfully", run.Id);
        return EngineStepResult.Completed(run, run.ResultSummary);
    }

    private async Task<EngineStepResult> TerminateRunAsync(
        ExecutionRun run, ExecutionRunStatus status, string reason, CancellationToken ct = default)
    {
        run.Status = status;
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedAt = DateTime.UtcNow;
        run.ErrorMessage = reason;

        // Cancel all non-terminal tasks
        foreach (var task in run.Tasks.Where(t => !IsTaskTerminal(t.Status)))
        {
            task.Status = ExecutionTaskStatus.Cancelled;
            task.UpdatedAt = DateTime.UtcNow;
        }

        _db.JournalEntries.Add(JournalEntry.Create(
            run.Id, JournalEntryType.Error,
            $"Run terminated: {reason}"));

        await _db.SaveChangesAsync(ct);
        Log.Warning("[Engine] Run {RunId} terminated: {Reason}", run.Id, reason);

        return status == ExecutionRunStatus.BudgetExhausted
            ? EngineStepResult.BudgetExhausted(run, reason)
            : EngineStepResult.Error(reason);
    }

    private static bool IsTerminal(ExecutionRunStatus status) =>
        status is ExecutionRunStatus.Succeeded or ExecutionRunStatus.Failed
            or ExecutionRunStatus.Cancelled or ExecutionRunStatus.BudgetExhausted
            or ExecutionRunStatus.TimedOut;

    private static bool IsTaskTerminal(ExecutionTaskStatus status) =>
        status is ExecutionTaskStatus.Succeeded or ExecutionTaskStatus.Failed
            or ExecutionTaskStatus.Cancelled or ExecutionTaskStatus.Skipped;

    private static string Truncate(string? s, int max) =>
        s is null ? "" : s.Length <= max ? s : s[..max] + "...";
}

/// <summary>Result of one engine step.</summary>
public record EngineStepResult
{
    public bool ShouldContinue { get; init; }
    public bool IsComplete { get; init; }
    public bool IsWaitingForApproval { get; init; }
    public bool IsBudgetExhausted { get; init; }
    public bool IsError { get; init; }
    public ExecutionRun? Run { get; init; }
    public ExecutionTask? Task { get; init; }
    public ApprovalRequest? Approval { get; init; }
    public string? Message { get; init; }

    public static EngineStepResult Continue(ExecutionRun? run, ExecutionTask? task, string message) =>
        new() { ShouldContinue = true, Run = run, Task = task, Message = message };

    public static EngineStepResult Completed(ExecutionRun? run, string? summary) =>
        new() { IsComplete = true, Run = run, Message = summary };

    public static EngineStepResult WaitingForApproval(ExecutionRun run, ApprovalRequest approval) =>
        new() { IsWaitingForApproval = true, Run = run, Approval = approval, Message = approval.ProposedAction };

    public static EngineStepResult Waiting(ExecutionRun run, string message) =>
        new() { Run = run, Message = message };

    public static EngineStepResult BudgetExhausted(ExecutionRun run, string reason) =>
        new() { IsBudgetExhausted = true, Run = run, Message = reason };

    public static EngineStepResult Error(string message) =>
        new() { IsError = true, Message = message };
}
