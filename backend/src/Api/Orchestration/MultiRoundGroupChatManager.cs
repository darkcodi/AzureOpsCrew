using AzureOpsCrew.Api.Settings;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Multi-round Manager-first orchestration with structured delegation and tool enforcement.
///
/// Flow per round:
///   1. Manager speaks (analyzes / delegates via orchestrator_delegate_tasks / synthesizes)
///   2. Parse delegated tasks from function calls OR text mentions (fallback)
///   3. Each delegated worker speaks (evidence-first)
///   4. Enforce tool usage: if task requires_tools and worker didn't call tools → reject + retry
///   5. After all workers spoke, Manager speaks again (next round) unless done.
///
/// NEW FEATURES (behind feature flags):
///   - Structured delegation via orchestrator_delegate_tasks tool
///   - Tool enforcement with configurable retries
///   - Direct addressing (@DevOps, @Developer) bypass
///   - Integration with inventory tool and artifact system
///
/// Termination conditions:
///   - Manager responds with [RESOLVED] or [APPROVAL REQUIRED]
///   - MaxRoundsPerRun reached
///   - MaxConsecutiveNonToolTurns reached (consecutive, not cumulative)
///   - MaximumIterationCount safety limit
///   - Tool enforcement failure after MaxMissingToolRetries
/// </summary>
public class MultiRoundGroupChatManager : GroupChatManager
{
    private readonly IReadOnlyList<AIAgent> _agents;
    private readonly AIAgent _manager;
    private readonly OrchestrationSettings _settings;
    private readonly RunContext _runContext;

    private readonly Queue<AIAgent> _delegationQueue = new();
    private int _round;
    private int _consecutiveNonToolTurns;
    private bool _managerHasSpokenThisRound;
    private bool _delegationParsedThisRound;
    private bool _terminated;
    
    // Tool enforcement state
    private AIAgent? _currentWorker;
    private bool _workerResponsePending;
    private bool _workerUsedToolsThisTurn;
    
    // Track if we're in direct addressing mode (bypass Manager)
    private bool _directAddressingMode;
    private AIAgent? _directAddressTarget;

    public MultiRoundGroupChatManager(
        IReadOnlyList<AIAgent> agents,
        AIAgent managerAgent,
        OrchestrationSettings settings,
        RunContext runContext)
    {
        _agents = agents;
        _manager = managerAgent;
        _settings = settings;
        _runContext = runContext;

        // Safety: Manager + N workers per round × max rounds + buffer + retry allowance
        MaximumIterationCount = (agents.Count + 1) * settings.MaxRoundsPerRun + 
            3 + settings.MaxMissingToolRetries * agents.Count;

        // Check for direct addressing
        if (_settings.EnableDirectAddressing && _runContext.DirectAddress?.IsDirect == true)
        {
            SetupDirectAddressingMode();
        }
    }

    /// <summary>
    /// Set up direct addressing mode when user uses @Agent syntax.
    /// </summary>
    private void SetupDirectAddressingMode()
    {
        var targetName = _runContext.DirectAddress?.AddressedTo;
        if (string.IsNullOrEmpty(targetName)) return;

        var targetAgent = _agents.FirstOrDefault(a => 
            a.Name?.Equals(targetName, StringComparison.OrdinalIgnoreCase) == true);
        
        if (targetAgent != null && !ReferenceEquals(targetAgent, _manager))
        {
            _directAddressingMode = true;
            _directAddressTarget = targetAgent;
            _delegationQueue.Enqueue(targetAgent);
            _managerHasSpokenThisRound = true; // Skip Manager in first round
            _round = 1;
            Log.Information("[Run {RunId}] Direct addressing mode: routing to {Agent}", 
                _runContext.RunId, targetName);
        }
    }

    protected override ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        if (_terminated)
        {
            Log.Information("[Run {RunId}] Orchestrator terminated, returning Manager as fallback", _runContext.RunId);
            return ValueTask.FromResult(_manager);
        }

        // Direct addressing mode: skip Manager, go straight to target
        if (_directAddressingMode && _round == 1 && _delegationQueue.Count > 0)
        {
            var target = _delegationQueue.Dequeue();
            _currentWorker = target;
            _workerResponsePending = true;
            _workerUsedToolsThisTurn = false;
            Log.Information("[Run {RunId}] Direct addressing: {Agent} speaks", _runContext.RunId, target.Name);
            return ValueTask.FromResult(target);
        }

        // Start of a new round (or very first turn): Manager speaks
        if (!_managerHasSpokenThisRound)
        {
            _managerHasSpokenThisRound = true;
            _round++;
            Log.Information("[Run {RunId}] Round {Round}: Manager speaks", _runContext.RunId, _round);
            _runContext.RecordAgentTurn("Manager", false);
            return ValueTask.FromResult(_manager);
        }

        // Dequeue next delegated worker
        if (_delegationQueue.Count > 0)
        {
            var next = _delegationQueue.Dequeue();
            _currentWorker = next;
            _workerResponsePending = true;
            _workerUsedToolsThisTurn = false;
            Log.Information("[Run {RunId}] Round {Round}: selecting {Agent}", _runContext.RunId, _round, next.Name);
            return ValueTask.FromResult(next);
        }

        // All workers done for this round → start next round (Manager speaks again)
        if (_round < _settings.MaxRoundsPerRun)
        {
            _managerHasSpokenThisRound = false;
            _delegationParsedThisRound = false;
            _currentWorker = null;
            return SelectNextAgentAsync(history, cancellationToken);
        }

        // Max rounds reached
        Log.Warning("[Run {RunId}] Max rounds ({Max}) reached, terminating", _runContext.RunId, _settings.MaxRoundsPerRun);
        _terminated = true;
        return ValueTask.FromResult(_manager);
    }

    protected override ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        if (_terminated)
            return ValueTask.FromResult(true);

        // Don't terminate before Manager has spoken this round (unless direct addressing)
        if (!_managerHasSpokenThisRound && !_directAddressingMode)
            return ValueTask.FromResult(false);

        // Tool enforcement check: if worker pending and required tools but no tools used
        if (_workerResponsePending && _settings.EnableToolEnforcement)
        {
            var enforcementResult = CheckToolEnforcement(history);
            if (enforcementResult == ToolEnforcementResult.Retry)
            {
                // Re-queue worker for retry
                if (_currentWorker != null)
                {
                    _delegationQueue.Enqueue(_currentWorker);
                    _workerResponsePending = false;
                    Log.Warning("[Run {RunId}] Tool enforcement: worker {Agent} retry {Count}/{Max}",
                        _runContext.RunId, _currentWorker.Name, 
                        _runContext.CurrentTaskMissingToolRetries, _settings.MaxMissingToolRetries);
                }
                return ValueTask.FromResult(false);
            }
            else if (enforcementResult == ToolEnforcementResult.Fail)
            {
                Log.Error("[Run {RunId}] Tool enforcement: worker failed after max retries, terminating",
                    _runContext.RunId);
                _runContext.TransitionTo(RunStatus.Failed, "Worker failed to use required tools");
                _terminated = true;
                return ValueTask.FromResult(true);
            }
            // ToolEnforcementResult.Pass - continue normally
            _workerResponsePending = false;
        }

        // Parse delegation ONCE per round after Manager spoke
        if (!_delegationParsedThisRound && !_directAddressingMode)
        {
            _delegationParsedThisRound = true;
            ParseDelegation(history);

            // Track if Manager used evidence-based conclusion markers
            var managerText = GetLastManagerMessage(history) ?? "";
            var isTerminalResponse =
                managerText.Contains("[RESOLVED]", StringComparison.OrdinalIgnoreCase) ||
                managerText.Contains("[APPROVAL REQUIRED]", StringComparison.OrdinalIgnoreCase) ||
                managerText.Contains("[APPROVAL_REQUIRED]", StringComparison.OrdinalIgnoreCase);

            if (isTerminalResponse && _delegationQueue.Count == 0)
            {
                Log.Information("[Run {RunId}] Manager used terminal marker, concluding run", _runContext.RunId);
                _runContext.TransitionTo(
                    managerText.Contains("[RESOLVED]", StringComparison.OrdinalIgnoreCase)
                        ? RunStatus.Resolved : RunStatus.WaitingForApproval);
                _terminated = true;
                return ValueTask.FromResult(true);
            }

            // No delegation detected → Manager answered directly (no workers needed)
            if (_delegationQueue.Count == 0)
            {
                if (_round == 1)
                {
                    Log.Warning("[Run {RunId}] Round 1 terminated without delegation — Manager did not delegate and no fallback worker found", _runContext.RunId);
                }
                else
                {
                    Log.Information("[Run {RunId}] No delegation in round {Round}, Manager answered directly", _runContext.RunId, _round);
                }
                _terminated = true;
                return ValueTask.FromResult(true);
            }
        }

        // Direct addressing: terminate after target agent responds (unless they need more rounds)
        if (_directAddressingMode && _delegationQueue.Count == 0)
        {
            var targetText = GetLastWorkerMessage(history, _directAddressTarget?.Name);
            var hasEvidence = targetText?.Contains("[EVIDENCE]", StringComparison.OrdinalIgnoreCase) == true;
            
            if (hasEvidence || _round >= _settings.MaxRoundsPerRun)
            {
                Log.Information("[Run {RunId}] Direct addressing complete for {Agent}", 
                    _runContext.RunId, _directAddressTarget?.Name);
                _runContext.TransitionTo(RunStatus.Resolved, "Direct addressing completed");
                _terminated = true;
                return ValueTask.FromResult(true);
            }
        }

        // Check for non-tool turn limit (prevents endless chat without progress)
        if (_consecutiveNonToolTurns >= _settings.MaxConsecutiveNonToolTurns)
        {
            Log.Warning("[Run {RunId}] {Max} consecutive non-tool turns, forcing stop", _runContext.RunId, _settings.MaxConsecutiveNonToolTurns);
            _terminated = true;
            return ValueTask.FromResult(true);
        }

        // Workers still pending in this round
        if (_delegationQueue.Count > 0)
            return ValueTask.FromResult(false);

        // All workers done this round → continue to next round (Manager will speak)
        return ValueTask.FromResult(false);
    }

    /// <summary>
    /// Check tool enforcement for the current worker's response.
    /// </summary>
    private ToolEnforcementResult CheckToolEnforcement(IReadOnlyList<ChatMessage> history)
    {
        // Check if current task requires tools
        var task = _runContext.CurrentTask;
        if (task == null || !task.Value.Task.RequiresTools)
        {
            return ToolEnforcementResult.Pass;
        }

        // Check if worker used any tools by examining the history
        // Look for tool calls from the current worker in recent messages
        var workerUsedTools = _workerUsedToolsThisTurn || DetectWorkerToolUsage(history);
        
        if (workerUsedTools)
        {
            _runContext.CompleteCurrentTask(true, "Task completed with tool calls");
            return ToolEnforcementResult.Pass;
        }

        // Worker didn't use tools but task required them
        _runContext.RecordMissingToolRetry();

        if (_runContext.CurrentTaskMissingToolRetries >= _settings.MaxMissingToolRetries)
        {
            _runContext.CompleteCurrentTask(false, error: "Failed to use required tools after max retries");
            return ToolEnforcementResult.Fail;
        }

        // Inject system message to prompt retry
        // (The actual injection would happen at a higher level that has access to modify history)
        return ToolEnforcementResult.Retry;
    }

    /// <summary>
    /// Detect if the current worker used any tools by examining chat history.
    /// Looks for FunctionCallContent or FunctionResultContent from the worker.
    /// </summary>
    private bool DetectWorkerToolUsage(IReadOnlyList<ChatMessage> history)
    {
        if (_currentWorker == null) return false;

        var workerName = _currentWorker.Name;
        
        // Scan recent history for tool usage by this worker
        // Look backwards from the end for efficiency
        for (var i = history.Count - 1; i >= 0 && i >= history.Count - 10; i--)
        {
            var msg = history[i];
            
            // Check if this message is from current worker or a tool result
            var isFromWorker = msg.AuthorName?.Equals(workerName, StringComparison.OrdinalIgnoreCase) == true;
            var isToolMessage = msg.Role.Value == "tool" || msg.Role == ChatRole.Tool;
            
            if (!isFromWorker && !isToolMessage) continue;
            
            // Check for FunctionCallContent (tool request) or FunctionResultContent (tool response)
            foreach (var content in msg.Contents)
            {
                if (content is FunctionCallContent functionCall)
                {
                    Log.Debug("[ToolEnforcement] Detected tool call from {Worker}: {Tool}",
                        workerName, functionCall.Name);
                    _workerUsedToolsThisTurn = true;
                    _runContext.RecordToolCall(functionCall.Name, workerName, true);
                    return true;
                }
                if (content is FunctionResultContent)
                {
                    // Tool result means a tool was called
                    _workerUsedToolsThisTurn = true;
                    return true;
                }
            }
        }

        return false;
    }

    private enum ToolEnforcementResult { Pass, Retry, Fail }

    /// <summary>
    /// Should be called externally when a tool call is observed in the stream,
    /// to reset the non-tool turn counter.
    /// </summary>
    public void RecordToolUsage(string agentName, string toolName, bool success)
    {
        _consecutiveNonToolTurns = 0;
        _workerUsedToolsThisTurn = true;
        _runContext.RecordToolCall(toolName, agentName, success);
        
        // Track specific orchestrator tools
        if (toolName == "orchestrator_delegate_tasks")
        {
            Log.Information("[Run {RunId}] Structured delegation tool called", _runContext.RunId);
        }
        else if (toolName == "inventory_list_all_resources")
        {
            _runContext.RecordInventorySource();
            Log.Information("[Run {RunId}] Inventory tool called", _runContext.RunId);
        }
    }

    /// <summary>
    /// Should be called externally when an agent responds without tools.
    /// </summary>
    public void RecordNonToolTurn(string agentName)
    {
        _consecutiveNonToolTurns++;
        _runContext.RecordAgentTurn(agentName, usedTools: false);
    }

    /// <summary>
    /// Parse delegation from Manager's response.
    /// Structured delegation (EnableStructuredDelegation=true): look for orchestrator_delegate_tasks calls.
    /// Fallback (text-based): parse agent names mentioned in text.
    /// </summary>
    private void ParseDelegation(IReadOnlyList<ChatMessage> history)
    {
        // 1. Try structured delegation first (if enabled)
        if (_settings.EnableStructuredDelegation)
        {
            if (TryParseStructuredDelegation(history))
            {
                Log.Information("[Run {RunId}] Round {Round}: structured delegation parsed, {Count} tasks queued",
                    _runContext.RunId, _round, _delegationQueue.Count);
                return;
            }
        }

        // 2. Check RunContext for already-queued delegated tasks
        if (_runContext.HasDelegatedTasks())
        {
            ProcessQueuedDelegatedTasks();
            if (_delegationQueue.Count > 0)
            {
                Log.Information("[Run {RunId}] Round {Round}: {Count} tasks from delegation queue",
                    _runContext.RunId, _round, _delegationQueue.Count);
                return;
            }
        }

        // 3. Fallback: text-based parsing (legacy behavior)
        ParseTextBasedDelegation(history);
    }

    /// <summary>
    /// Try to parse structured delegation from orchestrator_delegate_tasks function calls.
    /// </summary>
    private bool TryParseStructuredDelegation(IReadOnlyList<ChatMessage> history)
    {
        var lastManagerMessage = GetLastManagerMessageObject(history);
        if (lastManagerMessage == null) return false;

        // Look for FunctionCallContent in the message
        foreach (var content in lastManagerMessage.Contents)
        {
            if (content is FunctionCallContent functionCall &&
                functionCall.Name == "orchestrator_delegate_tasks")
            {
                try
                {
                    var argsJson = functionCall.Arguments?.ToString();
                    if (string.IsNullOrEmpty(argsJson)) continue;

                    // Parse the tasks from the function call arguments
                    var request = JsonSerializer.Deserialize<DelegationRequest>(argsJson);
                    if (request?.Tasks == null || request.Tasks.Count == 0) continue;

                    foreach (var task in request.Tasks)
                    {
                        var agent = FindAgentByName(task.Assignee);
                        if (agent != null)
                        {
                            var taskId = Guid.NewGuid().ToString();
                            _runContext.QueueDelegatedTask(task, taskId);
                            _delegationQueue.Enqueue(agent);
                            Log.Information("[Run {RunId}] Structured delegation: {Agent} → {Intent}",
                                _runContext.RunId, task.Assignee, task.Intent);
                        }
                        else
                        {
                            Log.Warning("[Run {RunId}] Unknown assignee in delegation: {Assignee}",
                                _runContext.RunId, task.Assignee);
                        }
                    }
                    return _delegationQueue.Count > 0;
                }
                catch (JsonException ex)
                {
                    Log.Warning(ex, "[Run {RunId}] Failed to parse structured delegation", _runContext.RunId);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Process already-queued delegated tasks from RunContext.
    /// </summary>
    private void ProcessQueuedDelegatedTasks()
    {
        while (true)
        {
            var nextTask = _runContext.DequeueNextTask();
            if (nextTask == null) break;

            var agent = FindAgentByName(nextTask.Value.Task.Assignee);
            if (agent != null)
            {
                _delegationQueue.Enqueue(agent);
            }
        }
    }

    /// <summary>
    /// Legacy text-based delegation parsing.
    /// </summary>
    private void ParseTextBasedDelegation(IReadOnlyList<ChatMessage> history)
    {
        var managerText = GetLastManagerMessage(history);

        if (string.IsNullOrWhiteSpace(managerText))
        {
            Log.Warning("[Run {RunId}] Manager's message not found in history", _runContext.RunId);
            return;
        }

        // Sort workers by name length (longest first) to avoid substring issues
        var workers = _agents
            .Where(a => !ReferenceEquals(a, _manager))
            .OrderByDescending(a => a.Name.Length)
            .ToList();

        var remainingText = managerText!;
        foreach (var agent in workers)
        {
            if (agent.Name is not null && remainingText.Contains(agent.Name, StringComparison.OrdinalIgnoreCase))
            {
                _delegationQueue.Enqueue(agent);
                
                // Create a generic delegated task for text-based delegation
                var task = new DelegatedTask
                {
                    Assignee = agent.Name,
                    Intent = TaskIntents.Generic,
                    Goal = "Task from text-based delegation",
                    RequiresTools = true
                };
                _runContext.QueueDelegatedTask(task, Guid.NewGuid().ToString());
                
                Log.Information("[Run {RunId}] Round {Round}: text-based delegating to {Agent}", 
                    _runContext.RunId, _round, agent.Name);

                remainingText = Regex.Replace(
                    remainingText,
                    Regex.Escape(agent.Name),
                    "",
                    RegexOptions.IgnoreCase);
            }
        }

        // Fallback: if Manager didn't mention any worker names but this is Round 1,
        // check if the ORIGINAL user message directly addresses an agent.
        if (_delegationQueue.Count == 0 && _round == 1)
        {
            var userMessage = GetLastUserMessage(history);
            if (!string.IsNullOrWhiteSpace(userMessage))
            {
                foreach (var agent in workers)
                {
                    if (agent.Name is not null && userMessage.Contains(agent.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        _delegationQueue.Enqueue(agent);
                        _runContext.QueueDelegatedTask(new DelegatedTask
                        {
                            Assignee = agent.Name,
                            Intent = TaskIntents.Generic,
                            Goal = "User-addressed task",
                            RequiresTools = true
                        }, Guid.NewGuid().ToString());
                        Log.Information("[Run {RunId}] Round {Round}: user-addressed delegation to {Agent}", 
                            _runContext.RunId, _round, agent.Name);
                    }
                }
            }

            // If still no delegation found on round 1, force-delegate to DevOps as default worker
            if (_delegationQueue.Count == 0)
            {
                var devOpsAgent = workers.FirstOrDefault(a =>
                    a.Name is not null && a.Name.Contains("DevOps", StringComparison.OrdinalIgnoreCase));
                if (devOpsAgent is not null)
                {
                    _delegationQueue.Enqueue(devOpsAgent);
                    _runContext.QueueDelegatedTask(new DelegatedTask
                    {
                        Assignee = devOpsAgent.Name!,
                        Intent = TaskIntents.Generic,
                        Goal = "Default fallback task (Manager failed to delegate)",
                        RequiresTools = true
                    }, Guid.NewGuid().ToString());
                    Log.Information("[Run {RunId}] Round {Round}: force-delegating to DevOps (Manager failed to delegate)", 
                        _runContext.RunId, _round);
                }
            }
        }

        if (_delegationQueue.Count == 0)
        {
            Log.Information("[Run {RunId}] Round {Round}: no delegation detected", _runContext.RunId, _round);
        }
    }

    private AIAgent? FindAgentByName(string name)
    {
        return _agents.FirstOrDefault(a => 
            a.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
    }

    private string? GetLastUserMessage(IReadOnlyList<ChatMessage> history)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (history[i].Role == ChatRole.User)
                return history[i].Text;
        }
        return null;
    }

    private string? GetLastManagerMessage(IReadOnlyList<ChatMessage> history)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (string.Equals(history[i].AuthorName, _manager.Name, StringComparison.OrdinalIgnoreCase))
                return history[i].Text;
        }
        return null;
    }

    private ChatMessage? GetLastManagerMessageObject(IReadOnlyList<ChatMessage> history)
    {
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (string.Equals(history[i].AuthorName, _manager.Name, StringComparison.OrdinalIgnoreCase))
                return history[i];
        }
        return null;
    }

    private string? GetLastWorkerMessage(IReadOnlyList<ChatMessage> history, string? workerName)
    {
        if (string.IsNullOrEmpty(workerName)) return null;
        
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (string.Equals(history[i].AuthorName, workerName, StringComparison.OrdinalIgnoreCase))
                return history[i].Text;
        }
        return null;
    }
}
