using AzureOpsCrew.Api.Settings;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Serilog;
using System.Text.RegularExpressions;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// Multi-round Manager-first orchestration.
///
/// Flow per round:
///   1. Manager speaks (analyzes / delegates / synthesizes)
///   2. Parse which workers were mentioned → queue them
///   3. Each queued worker speaks (evidence-first)
///   4. After all workers spoke, Manager speaks again (next round) unless done.
///
/// Termination conditions:
///   - Manager responds without mentioning any worker → done
///   - Manager says [RESOLVED] or [APPROVAL REQUIRED] → done
///   - MaxRoundsPerRun reached
///   - MaxConsecutiveNonToolTurns reached
///   - MaximumIterationCount safety limit
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

        // Safety: Manager + N workers per round × max rounds + buffer
        MaximumIterationCount = (agents.Count + 1) * settings.MaxRoundsPerRun + 3;
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
            Log.Information("[Run {RunId}] Round {Round}: selecting {Agent}", _runContext.RunId, _round, next.Name);
            return ValueTask.FromResult(next);
        }

        // All workers done for this round → start next round (Manager speaks again)
        if (_round < _settings.MaxRoundsPerRun)
        {
            _managerHasSpokenThisRound = false;
            _delegationParsedThisRound = false;
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

        // Don't terminate before Manager has spoken this round
        if (!_managerHasSpokenThisRound)
            return ValueTask.FromResult(false);

        // Parse delegation ONCE per round after Manager spoke
        if (!_delegationParsedThisRound)
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
            // But on Round 1, this should not happen if ParseDelegation force-delegates.
            if (_delegationQueue.Count == 0)
            {
                // On round 1, if the Manager STILL failed to delegate (shouldn't happen with fallback),
                // at least log a critical warning since this means the system isn't working properly.
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
    /// Should be called externally when a tool call is observed in the stream,
    /// to reset the non-tool turn counter.
    /// </summary>
    public void RecordToolUsage(string agentName, string toolName, bool success)
    {
        _consecutiveNonToolTurns = 0;
        _runContext.RecordToolCall(toolName, agentName, success);
    }

    /// <summary>
    /// Should be called externally when an agent responds without tools.
    /// </summary>
    public void RecordNonToolTurn(string agentName)
    {
        _consecutiveNonToolTurns++;
        _runContext.RecordAgentTurn(agentName, usedTools: false);
    }

    private void ParseDelegation(IReadOnlyList<ChatMessage> history)
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
                Log.Information("[Run {RunId}] Round {Round}: delegating to {Agent}", _runContext.RunId, _round, agent.Name);

                remainingText = Regex.Replace(
                    remainingText,
                    Regex.Escape(agent.Name),
                    "",
                    RegexOptions.IgnoreCase);
            }
        }

        // Fallback: if Manager didn't mention any worker names but this is Round 1,
        // check if the ORIGINAL user message directly addresses an agent.
        // e.g. "devops, check Azure resources" → delegate to DevOps even if Manager forgot.
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
                        Log.Information("[Run {RunId}] Round {Round}: user-addressed delegation to {Agent}", _runContext.RunId, _round, agent.Name);
                    }
                }
            }

            // If still no delegation found on round 1, force-delegate to DevOps as default worker
            // for infrastructure/Azure requests (the most common case)
            if (_delegationQueue.Count == 0)
            {
                var devOpsAgent = workers.FirstOrDefault(a =>
                    a.Name is not null && a.Name.Contains("DevOps", StringComparison.OrdinalIgnoreCase));
                if (devOpsAgent is not null)
                {
                    _delegationQueue.Enqueue(devOpsAgent);
                    Log.Information("[Run {RunId}] Round {Round}: force-delegating to DevOps (Manager failed to delegate)", _runContext.RunId, _round);
                }
            }
        }

        if (_delegationQueue.Count == 0)
        {
            Log.Information("[Run {RunId}] Round {Round}: no delegation detected", _runContext.RunId, _round);
        }
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
}
