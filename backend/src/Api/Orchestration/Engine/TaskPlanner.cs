using AzureOpsCrew.Api.Settings;
using AzureOpsCrew.Domain.Execution;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;

namespace AzureOpsCrew.Api.Orchestration.Engine;

/// <summary>
/// Planner layer: builds initial plan, decomposes to tasks, manages dependencies,
/// decides when to replan and why, chooses routing (DevOps vs Developer vs Manager step).
/// Uses LLM to generate structured plans from user goals.
/// </summary>
public class TaskPlanner
{
    private readonly AzureOpsCrewContext _db;
    private readonly ExecutionEngineSettings _settings;
    private readonly IChatClient? _plannerChatClient;

    public TaskPlanner(
        AzureOpsCrewContext db,
        IOptions<ExecutionEngineSettings> settings,
        IChatClient? plannerChatClient = null)
    {
        _db = db;
        _settings = settings.Value;
        _plannerChatClient = plannerChatClient;
    }

    /// <summary>
    /// Create an initial plan from the user request. Decomposes into subtasks.
    /// </summary>
    public async Task<PlanResult> CreatePlanAsync(ExecutionRun run, CancellationToken ct = default)
    {
        Log.Information("[Planner] Creating plan for run {RunId}: {Request}",
            run.Id, Truncate(run.UserRequest, 100));

        try
        {
            List<PlannedTask> tasks;

            if (_plannerChatClient is not null)
            {
                tasks = await GeneratePlanWithLlmAsync(run.UserRequest, null, ct);
            }
            else
            {
                // Fallback: heuristic decomposition
                tasks = GenerateHeuristicPlan(run.UserRequest);
            }

            if (tasks.Count == 0)
            {
                return PlanResult.Failure("Planner generated no tasks");
            }

            var planDescription = string.Join("\n", tasks.Select((t, i) => $"{i + 1}. [{t.Type}] {t.Title} → {t.Agent}"));
            run.InitialPlan = planDescription;
            run.CurrentPlan = planDescription;
            run.PlanRevision = 0;
            run.UpdatedAt = DateTime.UtcNow;

            // Create task entities
            var createdTasks = new List<ExecutionTask>();
            foreach (var planned in tasks)
            {
                var task = ExecutionTask.Create(
                    run.Id,
                    planned.Title,
                    planned.Type,
                    planned.Agent);
                task.Description = planned.Description;
                task.Goal = planned.Goal;
                task.Priority = planned.Priority;
                task.Status = ExecutionTaskStatus.Ready;

                _db.ExecutionTasks.Add(task);
                createdTasks.Add(task);
            }

            // Resolve dependencies (by index matching)
            for (int i = 0; i < tasks.Count; i++)
            {
                if (tasks[i].DependsOnIndices.Count > 0)
                {
                    var depIds = tasks[i].DependsOnIndices
                        .Where(idx => idx >= 0 && idx < createdTasks.Count)
                        .Select(idx => createdTasks[idx].Id)
                        .ToList();

                    if (depIds.Count > 0)
                    {
                        createdTasks[i].SetDependencies(depIds);
                        createdTasks[i].Status = ExecutionTaskStatus.Blocked;
                    }
                }
            }

            _db.JournalEntries.Add(JournalEntry.Create(
                run.Id, JournalEntryType.PlanCreated,
                $"Initial plan created with {tasks.Count} tasks",
                detail: planDescription));

            await _db.SaveChangesAsync(ct);
            Log.Information("[Planner] Plan created with {Count} tasks for run {RunId}", tasks.Count, run.Id);

            return PlanResult.Ok(planDescription);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Planner] Failed to create plan for run {RunId}", run.Id);
            return PlanResult.Failure($"Planning error: {ex.Message}");
        }
    }

    /// <summary>
    /// Replan: revise the current plan based on new evidence/failures.
    /// Closes/skips obsolete tasks, creates new ones, preserves history.
    /// </summary>
    public async Task<PlanResult> ReplanAsync(ExecutionRun run, string reason, CancellationToken ct = default)
    {
        Log.Information("[Planner] Replanning run {RunId}: {Reason}", run.Id, reason);

        try
        {
            // Gather context: completed tasks + their results
            var completedTasks = run.Tasks
                .Where(t => t.Status == ExecutionTaskStatus.Succeeded)
                .Select(t => $"✅ {t.Title}: {t.ResultSummary}")
                .ToList();

            var failedTasks = run.Tasks
                .Where(t => t.Status == ExecutionTaskStatus.Failed)
                .Select(t => $"❌ {t.Title}: {t.ResultSummary}")
                .ToList();

            var context = $"""
                Original goal: {run.UserRequest}
                Previous plan (revision {run.PlanRevision}): {run.CurrentPlan}
                Replan reason: {reason}
                Completed tasks:
                {string.Join("\n", completedTasks)}
                Failed tasks:
                {string.Join("\n", failedTasks)}
                """;

            List<PlannedTask> newTasks;
            if (_plannerChatClient is not null)
            {
                newTasks = await GeneratePlanWithLlmAsync(run.UserRequest, context, ct);
            }
            else
            {
                newTasks = GenerateReplanHeuristic(reason, run);
            }

            // Skip/cancel non-started tasks from old plan
            foreach (var task in run.Tasks.Where(t =>
                t.Status is ExecutionTaskStatus.Ready or ExecutionTaskStatus.Blocked or ExecutionTaskStatus.Created))
            {
                task.Status = ExecutionTaskStatus.Skipped;
                task.UpdatedAt = DateTime.UtcNow;
                task.ResultSummary = $"Skipped during replan #{run.PlanRevision + 1}";
            }

            // Create new tasks
            var created = new List<ExecutionTask>();
            foreach (var planned in newTasks)
            {
                var task = ExecutionTask.Create(run.Id, planned.Title, planned.Type, planned.Agent);
                task.Description = planned.Description;
                task.Goal = planned.Goal;
                task.Priority = planned.Priority;
                task.Status = ExecutionTaskStatus.Ready;
                _db.ExecutionTasks.Add(task);
                created.Add(task);
            }

            // Resolve deps for new tasks
            for (int i = 0; i < newTasks.Count; i++)
            {
                if (newTasks[i].DependsOnIndices.Count > 0)
                {
                    var depIds = newTasks[i].DependsOnIndices
                        .Where(idx => idx >= 0 && idx < created.Count)
                        .Select(idx => created[idx].Id)
                        .ToList();

                    if (depIds.Count > 0)
                    {
                        created[i].SetDependencies(depIds);
                        created[i].Status = ExecutionTaskStatus.Blocked;
                    }
                }
            }

            var newPlan = string.Join("\n", newTasks.Select((t, i) => $"{i + 1}. [{t.Type}] {t.Title} → {t.Agent}"));
            run.CurrentPlan = newPlan;
            run.UpdatedAt = DateTime.UtcNow;

            _db.JournalEntries.Add(JournalEntry.Create(
                run.Id, JournalEntryType.PlanRevised,
                $"Plan revised (#{run.PlanRevision + 1}): {reason}",
                detail: $"Old plan:\n{run.CurrentPlan}\n\nNew plan:\n{newPlan}"));

            await _db.SaveChangesAsync(ct);
            return PlanResult.Ok(newPlan);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[Planner] Replan failed for run {RunId}", run.Id);
            return PlanResult.Failure($"Replan error: {ex.Message}");
        }
    }

    // ─── LLM-based planning ───

    private async Task<List<PlannedTask>> GeneratePlanWithLlmAsync(
        string userRequest, string? replanContext, CancellationToken ct)
    {
        var systemPrompt = """
            You are a task planner for an Azure Ops multi-agent system.
            Decompose the user's request into a sequence of concrete tasks.
            
            Available agents:
            - devops: Azure infrastructure investigation, resource health, metrics, logs, Platform operations, remediation
            - developer: ADO pipelines/repos/work items, GitOps code changes, branch/commit/PR creation, code analysis, bug fixes
            
            Task types: Triage, Investigation, EvidenceCollection, Diagnosis, Planning, CodeFix, PullRequest, Approval, Deployment, Verification, Rollback, Summary
            
            Rules:
            - Start with Triage/Investigation tasks
            - Add EvidenceCollection to gather data before diagnosis
            - If deployment is needed, always add an Approval task before it
            - Always end with a Verification task
            - Keep task count between 3-10
            - Each task should be specific and actionable
            
            Respond ONLY with a JSON array of objects:
            [
              {
                "title": "short task title",
                "description": "what to do",
                "goal": "success criteria",
                "type": "TaskType",
                "agent": "agent-id",
                "priority": 1-10,
                "dependsOnIndices": [0, 1]
              }
            ]
            """;

        var userMessage = replanContext is not null
            ? $"REPLANNING:\n{replanContext}"
            : $"USER REQUEST: {userRequest}";

        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userMessage),
        };

        var response = await _plannerChatClient!.GetResponseAsync(messages, cancellationToken: ct);
        var text = response.Text ?? "";

        // Extract JSON from response
        var jsonStart = text.IndexOf('[');
        var jsonEnd = text.LastIndexOf(']');
        if (jsonStart < 0 || jsonEnd < 0)
        {
            Log.Warning("[Planner] LLM response did not contain JSON array, falling back to heuristic");
            return GenerateHeuristicPlan(userRequest);
        }

        var json = text[jsonStart..(jsonEnd + 1)];
        var planned = JsonSerializer.Deserialize<List<PlannedTaskDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        return planned?.Select(p => new PlannedTask
        {
            Title = p.Title ?? "Untitled task",
            Description = p.Description,
            Goal = p.Goal,
            Type = ParseTaskType(p.Type),
            Agent = p.Agent ?? "devops",
            Priority = p.Priority,
            DependsOnIndices = p.DependsOnIndices ?? [],
        }).ToList() ?? GenerateHeuristicPlan(userRequest);
    }

    // ─── Heuristic planning (no LLM) ───

    private List<PlannedTask> GenerateHeuristicPlan(string userRequest)
    {
        var lower = userRequest.ToLowerInvariant();
        var tasks = new List<PlannedTask>();

        // Always: triage
        tasks.Add(new PlannedTask
        {
            Title = "Triage: analyze and classify the request",
            Type = ExecutionTaskType.Triage,
            Agent = "devops",
            Goal = "Classify the issue: infrastructure, code, or deployment",
            Priority = 10,
        });

        // Investigation
        tasks.Add(new PlannedTask
        {
            Title = "Collect evidence from Azure resources",
            Type = ExecutionTaskType.EvidenceCollection,
            Agent = "devops",
            Goal = "Retrieve resource health, logs, and metrics",
            Priority = 9,
            DependsOnIndices = [0],
        });

        // Is it a deploy/pipeline issue?
        if (lower.Contains("deploy") || lower.Contains("pipeline") || lower.Contains("build") || lower.Contains("ci"))
        {
            tasks.Add(new PlannedTask
            {
                Title = "Check deployment pipelines and recent runs",
                Type = ExecutionTaskType.Investigation,
                Agent = "developer",
                Goal = "Identify pipeline failures or deployment issues",
                Priority = 8,
                DependsOnIndices = [0],
            });
        }

        // Diagnosis
        tasks.Add(new PlannedTask
        {
            Title = "Diagnose root cause from evidence",
            Type = ExecutionTaskType.Diagnosis,
            Agent = "devops",
            Goal = "Determine root cause with evidence",
            Priority = 7,
            DependsOnIndices = [tasks.Count - 1],
        });

        // Code fix scenario
        if (lower.Contains("code") || lower.Contains("fix") || lower.Contains("bug") || lower.Contains("error"))
        {
            var diagIdx = tasks.Count - 1;
            tasks.Add(new PlannedTask
            {
                Title = "Implement code fix",
                Type = ExecutionTaskType.CodeFix,
                Agent = "developer",
                Goal = "Create minimal fix for the identified issue",
                Priority = 6,
                DependsOnIndices = [diagIdx],
            });

            tasks.Add(new PlannedTask
            {
                Title = "Create pull request",
                Type = ExecutionTaskType.PullRequest,
                Agent = "developer",
                Goal = "Submit PR with the fix",
                Priority = 5,
                DependsOnIndices = [tasks.Count - 1],
            });
        }

        // Always: verification
        tasks.Add(new PlannedTask
        {
            Title = "Verify resolution",
            Type = ExecutionTaskType.Verification,
            Agent = "devops",
            Goal = "Confirm the issue is resolved",
            Priority = 2,
            DependsOnIndices = [tasks.Count - 1],
        });

        // Summary
        tasks.Add(new PlannedTask
        {
            Title = "Generate final summary",
            Type = ExecutionTaskType.Summary,
            Agent = "devops",
            Goal = "Summarize findings, actions taken, and results",
            Priority = 1,
            DependsOnIndices = [tasks.Count - 1],
        });

        return tasks;
    }

    private List<PlannedTask> GenerateReplanHeuristic(string reason, ExecutionRun run)
    {
        var lowerReason = reason.ToLowerInvariant();
        var tasks = new List<PlannedTask>();

        if (lowerReason.Contains("code") || lowerReason.Contains("bug"))
        {
            tasks.Add(new PlannedTask
            {
                Title = "Re-investigate: code-level analysis",
                Type = ExecutionTaskType.Investigation,
                Agent = "developer",
                Goal = "Analyze code for the identified issue pattern",
                Priority = 9,
            });
            tasks.Add(new PlannedTask
            {
                Title = "Implement code fix",
                Type = ExecutionTaskType.CodeFix,
                Agent = "developer",
                Goal = "Create fix based on new evidence",
                Priority = 8,
                DependsOnIndices = [0],
            });
        }
        else
        {
            tasks.Add(new PlannedTask
            {
                Title = "Re-investigate with different approach",
                Type = ExecutionTaskType.Investigation,
                Agent = "devops",
                Goal = "Gather additional evidence based on: " + reason,
                Priority = 9,
            });
        }

        tasks.Add(new PlannedTask
        {
            Title = "Verify after replan",
            Type = ExecutionTaskType.Verification,
            Agent = "devops",
            Goal = "Verify resolution after replanned actions",
            Priority = 2,
            DependsOnIndices = [tasks.Count - 1],
        });

        return tasks;
    }

    private static ExecutionTaskType ParseTaskType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return ExecutionTaskType.Generic;
        return type.ToLowerInvariant() switch
        {
            "triage" => ExecutionTaskType.Triage,
            "investigation" => ExecutionTaskType.Investigation,
            "evidencecollection" => ExecutionTaskType.EvidenceCollection,
            "diagnosis" => ExecutionTaskType.Diagnosis,
            "planning" => ExecutionTaskType.Planning,
            "codefix" => ExecutionTaskType.CodeFix,
            "pullrequest" => ExecutionTaskType.PullRequest,
            "approval" => ExecutionTaskType.Approval,
            "deployment" => ExecutionTaskType.Deployment,
            "verification" => ExecutionTaskType.Verification,
            "rollback" => ExecutionTaskType.Rollback,
            "summary" => ExecutionTaskType.Summary,
            _ => ExecutionTaskType.Generic,
        };
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}

// ─── DTOs ───

public record PlanResult(bool Success, string? Plan, string? Error)
{
    public static PlanResult Ok(string plan) => new(true, plan, null);
    public static PlanResult Failure(string error) => new(false, null, error);
}

public class PlannedTask
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public ExecutionTaskType Type { get; set; }
    public string Agent { get; set; } = "devops";
    public int Priority { get; set; }
    public List<int> DependsOnIndices { get; set; } = [];
}

internal class PlannedTaskDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public string? Type { get; set; }
    public string? Agent { get; set; }
    public int Priority { get; set; }
    public List<int>? DependsOnIndices { get; set; }
}
