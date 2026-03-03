using AzureOpsCrew.Api.Auth;
using AzureOpsCrew.Api.Orchestration.Engine;
using AzureOpsCrew.Domain.Execution;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AzureOpsCrew.Api.Endpoints;

public static class RunEndpoints
{
    public static void MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/runs").WithTags("Runs").RequireAuthorization();

        // POST /api/runs — create and start an execution run
        group.MapPost("/", async (
            [FromBody] CreateRunRequest request,
            TaskExecutionEngine engine,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            Log.Information("[RunEndpoint] Creating run for channel {ChannelId}: {Request}",
                request.ChannelId, request.UserRequest);

            var run = await engine.CreateRunAsync(
                request.ChannelId,
                userId,
                request.ThreadId ?? Guid.NewGuid().ToString(),
                request.UserRequest,
                ct);

            return Results.Created($"/api/runs/{run.Id}", MapRunToDto(run));
        });

        // GET /api/runs/{id} — get run status
        group.MapGet("/{id:guid}", async (
            [FromRoute] Guid id,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var run = await db.ExecutionRuns
                .Include(r => r.Tasks)
                .Include(r => r.Artifacts)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

            if (run is null) return Results.NotFound();
            return Results.Ok(MapRunToDto(run));
        });

        // POST /api/runs/{id}/step — execute one step
        group.MapPost("/{id:guid}/step", async (
            [FromRoute] Guid id,
            TaskExecutionEngine engine,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var run = await db.ExecutionRuns
                .Include(r => r.Tasks)
                .Include(r => r.Artifacts)
                .Include(r => r.Journal)
                .Include(r => r.ApprovalRequests)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

            if (run is null) return Results.NotFound();

            var result = await engine.RunStepAsync(run.Id, ct);

            // Reload run to get updated state
            await db.Entry(run).ReloadAsync(ct);
            await db.Entry(run).Collection(r => r.Tasks).LoadAsync(ct);
            await db.Entry(run).Collection(r => r.ApprovalRequests).LoadAsync(ct);

            var state = result.IsComplete ? "Completed"
                : result.IsWaitingForApproval ? "WaitingForApproval"
                : result.IsBudgetExhausted ? "BudgetExhausted"
                : result.IsError ? "Error"
                : result.ShouldContinue ? "Continue"
                : "Waiting";

            return Results.Ok(new StepResultDto(
                state,
                result.Message,
                MapRunToDto(run)));
        });

        // POST /api/runs/{id}/execute — run to completion
        group.MapPost("/{id:guid}/execute", async (
            [FromRoute] Guid id,
            TaskExecutionEngine engine,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var run = await db.ExecutionRuns
                .Include(r => r.Tasks)
                .Include(r => r.Artifacts)
                .Include(r => r.Journal)
                .Include(r => r.ApprovalRequests)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

            if (run is null) return Results.NotFound();

            await engine.RunToCompletionAsync(run.Id, ct);

            // Reload run after completion
            await db.Entry(run).ReloadAsync(ct);
            await db.Entry(run).Collection(r => r.Tasks).LoadAsync(ct);
            await db.Entry(run).Collection(r => r.Artifacts).LoadAsync(ct);
            await db.Entry(run).Collection(r => r.ApprovalRequests).LoadAsync(ct);

            return Results.Ok(MapRunToDto(run));
        });

        // POST /api/runs/{id}/approve — approve/deny an approval request
        group.MapPost("/{id:guid}/approve", async (
            [FromRoute] Guid id,
            [FromBody] ApprovalDecisionRequest request,
            TaskExecutionEngine engine,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var run = await db.ExecutionRuns
                .Include(r => r.Tasks)
                .Include(r => r.Artifacts)
                .Include(r => r.Journal)
                .Include(r => r.ApprovalRequests)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

            if (run is null) return Results.NotFound();

            var approval = run.ApprovalRequests
                .FirstOrDefault(a => a.Id == request.ApprovalRequestId && a.Status == ApprovalStatus.Pending);

            if (approval is null)
                return Results.BadRequest("No pending approval request with that ID");

            await engine.HandleApprovalAsync(run.Id, approval.Id, request.Approved, request.Reason, ct);

            return Results.Ok(MapRunToDto(run));
        });

        // GET /api/runs/{id}/tasks — get task tree
        group.MapGet("/{id:guid}/tasks", async (
            [FromRoute] Guid id,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var run = await db.ExecutionRuns
                .Include(r => r.Tasks)
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);

            if (run is null) return Results.NotFound();

            var tasks = run.Tasks
                .OrderBy(t => t.CreatedAt)
                .Select(MapTaskToDto)
                .ToList();

            return Results.Ok(tasks);
        });

        // GET /api/runs/{id}/journal — get execution journal
        group.MapGet("/{id:guid}/journal", async (
            [FromRoute] Guid id,
            [FromQuery] int? skip,
            [FromQuery] int? take,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var exists = await db.ExecutionRuns
                .AnyAsync(r => r.Id == id && r.UserId == userId, ct);

            if (!exists) return Results.NotFound();

            var query = db.JournalEntries
                .Where(j => j.RunId == id)
                .OrderBy(j => j.CreatedAt);

            var entries = await query
                .Skip(skip ?? 0)
                .Take(take ?? 100)
                .Select(j => new JournalEntryDto(
                    j.Id,
                    j.EntryType.ToString(),
                    j.Agent,
                    j.Message,
                    j.Detail,
                    j.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(entries);
        });

        // GET /api/runs/{id}/artifacts — get artifacts
        group.MapGet("/{id:guid}/artifacts", async (
            [FromRoute] Guid id,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var exists = await db.ExecutionRuns
                .AnyAsync(r => r.Id == id && r.UserId == userId, ct);

            if (!exists) return Results.NotFound();

            var artifacts = await db.Artifacts
                .Where(a => a.RunId == id)
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new ArtifactDto(
                    a.Id,
                    a.ArtifactType.ToString(),
                    a.Source,
                    a.CreatedBy,
                    a.Summary,
                    a.Tags,
                    a.CreatedAt))
                .ToListAsync(ct);

            return Results.Ok(artifacts);
        });

        // GET /api/runs/{id}/approvals — get pending approvals
        group.MapGet("/{id:guid}/approvals", async (
            [FromRoute] Guid id,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();
            var exists = await db.ExecutionRuns
                .AnyAsync(r => r.Id == id && r.UserId == userId, ct);

            if (!exists) return Results.NotFound();

            var approvals = await db.ApprovalRequests
                .Where(a => a.RunId == id)
                .OrderByDescending(a => a.RequestedAt)
                .Select(a => new ApprovalRequestDto(
                    a.Id,
                    a.ActionType,
                    a.ProposedAction,
                    a.Target,
                    a.RiskLevel.ToString(),
                    a.RollbackPlan,
                    a.VerificationPlan,
                    a.AffectedResources,
                    a.Status.ToString(),
                    a.DecisionReason,
                    a.RequestedAt,
                    a.RespondedAt))
                .ToListAsync(ct);

            return Results.Ok(approvals);
        });

        // GET /api/runs — list runs for current user
        group.MapGet("/", async (
            [FromQuery] int? skip,
            [FromQuery] int? take,
            AzureOpsCrewContext db,
            HttpContext http,
            CancellationToken ct) =>
        {
            var userId = http.User.GetRequiredUserId();

            var runs = await db.ExecutionRuns
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Skip(skip ?? 0)
                .Take(take ?? 20)
                .Select(r => new RunListItemDto(
                    r.Id,
                    r.ChannelId,
                    r.UserRequest,
                    r.Status.ToString(),
                    r.TotalSteps,
                    r.TotalToolCalls,
                    r.TotalReplans,
                    r.CreatedAt,
                    r.CompletedAt))
                .ToListAsync(ct);

            return Results.Ok(runs);
        });
    }

    // ─── DTOs ───

    private static RunDto MapRunToDto(ExecutionRun run) => new(
        run.Id,
        run.ChannelId,
        run.UserRequest,
        run.Status.ToString(),
        run.Goal,
        run.InitialPlan,
        run.CurrentPlan,
        run.PlanRevision,
        run.TotalSteps,
        run.TotalToolCalls,
        run.TotalReplans,
        run.ResultSummary,
        run.ErrorMessage,
        run.CreatedAt,
        run.CompletedAt,
        run.Tasks.Select(MapTaskToDto).ToList(),
        run.ApprovalRequests
            .Where(a => a.Status == ApprovalStatus.Pending)
            .Select(a => a.Id)
            .ToList());

    private static TaskDto MapTaskToDto(ExecutionTask t) => new(
        t.Id,
        t.ParentTaskId,
        t.Title,
        t.Description,
        t.TaskType.ToString(),
        t.AssignedAgent,
        t.Priority,
        t.Status.ToString(),
        t.Goal,
        t.ResultSummary,
        t.StepCount,
        t.RetryCount,
        t.CreatedAt,
        t.CompletedAt);
}

// ─── Request / Response records ───

public record CreateRunRequest(Guid ChannelId, string UserRequest, string? ThreadId = null);

public record ApprovalDecisionRequest(Guid ApprovalRequestId, bool Approved, string? Reason = null);

public record StepResultDto(string State, string? Message, RunDto Run);

public record RunDto(
    Guid Id, Guid ChannelId, string UserRequest, string Status,
    string? Goal, string? InitialPlan, string? CurrentPlan, int PlanRevision,
    int TotalSteps, int TotalToolCalls, int TotalReplans,
    string? ResultSummary, string? ErrorMessage,
    DateTime CreatedAt, DateTime? CompletedAt,
    List<TaskDto> Tasks, List<Guid> PendingApprovals);

public record TaskDto(
    Guid Id, Guid? ParentTaskId, string Title, string? Description,
    string TaskType, string? AssignedAgent, int Priority, string Status,
    string? Goal, string? ResultSummary,
    int StepCount, int RetryCount,
    DateTime CreatedAt, DateTime? CompletedAt);

public record RunListItemDto(
    Guid Id, Guid ChannelId, string UserRequest, string Status,
    int TotalSteps, int TotalToolCalls, int TotalReplans,
    DateTime CreatedAt, DateTime? CompletedAt);

public record JournalEntryDto(
    Guid Id, string EntryType, string? Agent, string Message,
    string? Detail, DateTime CreatedAt);

public record ArtifactDto(
    Guid Id, string ArtifactType, string? Source, string? CreatedBy,
    string? Summary, string? Tags, DateTime CreatedAt);

public record ApprovalRequestDto(
    Guid Id, string ActionType, string ProposedAction, string? Target,
    string RiskLevel, string? RollbackPlan, string? VerificationPlan,
    string? AffectedResources, string Status, string? DecisionReason,
    DateTime RequestedAt, DateTime? RespondedAt);
