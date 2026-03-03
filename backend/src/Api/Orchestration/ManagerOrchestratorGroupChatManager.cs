using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Serilog;
using System.Text.RegularExpressions;

namespace AzureOpsCrew.Api.Orchestration;

/// <summary>
/// A custom GroupChatManager that implements Manager-first orchestration.
///
/// Flow:
/// 1. Manager always speaks first (analyzes request, creates delegation plan)
/// 2. Based on Manager's response, only the mentioned agent(s) are activated
/// 3. Delegation is detected by looking for agent names in Manager's text
/// 4. If no agents are mentioned, Manager answered directly — no delegation needed
///
/// This replaces the default RoundRobinGroupChatManager which gives ALL agents
/// a turn regardless of relevance.
/// </summary>
public class ManagerOrchestratorGroupChatManager : GroupChatManager
{
    private readonly IReadOnlyList<AIAgent> _agents;
    private readonly AIAgent _manager;
    private readonly Queue<AIAgent> _delegationQueue = new();
    private bool _managerHasSpoken;
    private bool _delegationParsed;

    public ManagerOrchestratorGroupChatManager(
        IReadOnlyList<AIAgent> agents,
        AIAgent managerAgent)
    {
        _agents = agents;
        _manager = managerAgent;
        // Max: Manager + all workers + safety buffer
        MaximumIterationCount = agents.Count + 2;
    }

    protected override ValueTask<AIAgent> SelectNextAgentAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        // First turn: always Manager
        if (!_managerHasSpoken)
        {
            _managerHasSpoken = true;
            Log.Information("Orchestrator: selecting Manager as first speaker");
            return ValueTask.FromResult(_manager);
        }

        // Delegation was already parsed in ShouldTerminateAsync (called before this).
        // Just dequeue the next delegated agent.
        if (_delegationQueue.Count > 0)
        {
            var next = _delegationQueue.Dequeue();
            Log.Information("Orchestrator: selecting {Agent} as next speaker", next.Name);
            return ValueTask.FromResult(next);
        }

        // Shouldn't reach here (ShouldTerminate should have returned true), but safety fallback
        Log.Warning("Orchestrator: no more agents to select, returning Manager as fallback");
        return ValueTask.FromResult(_manager);
    }

    protected override ValueTask<bool> ShouldTerminateAsync(
        IReadOnlyList<ChatMessage> history,
        CancellationToken cancellationToken)
    {
        // Don't terminate before Manager speaks
        if (!_managerHasSpoken)
            return ValueTask.FromResult(false);

        // Parse delegation from Manager's response (only once, on first check after Manager spoke)
        if (!_delegationParsed)
        {
            _delegationParsed = true;
            ParseDelegation(history);
            Log.Information("Orchestrator: delegation parsed, {Count} agent(s) queued", _delegationQueue.Count);
        }

        // If queue is empty → either no delegation (Manager answered directly) or all workers done
        if (_delegationQueue.Count == 0)
        {
            Log.Information("Orchestrator: terminating (queue empty)");
            return ValueTask.FromResult(true);
        }

        // Workers still pending
        return ValueTask.FromResult(false);
    }

    private void ParseDelegation(IReadOnlyList<ChatMessage> history)
    {
        // Find Manager's last message in history
        string? managerText = null;
        for (int i = history.Count - 1; i >= 0; i--)
        {
            if (string.Equals(history[i].AuthorName, _manager.Name, StringComparison.OrdinalIgnoreCase))
            {
                managerText = history[i].Text;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(managerText))
        {
            Log.Warning("Orchestrator: Manager's message not found in history, no delegation");
            return;
        }

        Log.Information("Orchestrator: parsing delegation from Manager's response");

        // Sort workers by name length (longest first) to avoid substring issues
        // e.g., "Developer" must be matched before "Dev" prefix overlap
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
                Log.Information("Orchestrator: delegating to {Agent}", agent.Name);

                // Remove matched name to prevent substring false-positives
                // e.g., after matching "Developer", remove it so partial matches don't trigger
                remainingText = Regex.Replace(
                    remainingText,
                    Regex.Escape(agent.Name),
                    "",
                    RegexOptions.IgnoreCase);
            }
        }

        if (_delegationQueue.Count == 0)
        {
            Log.Information("Orchestrator: no delegation detected — Manager answered directly");
        }
    }
}
