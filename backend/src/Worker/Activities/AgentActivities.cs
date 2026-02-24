using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Temporalio.Activities;
using Worker.Models;

namespace Worker.Activities;

public class AgentActivities
{
    private readonly AzureOpsCrewContext _context;

    public AgentActivities(AzureOpsCrewContext context)
    {
        _context = context;
    }

    [Activity]
    public async Task<AgentSnapshotDto> LoadSnapshotAsync(Guid agentId)
    {
        var snapshot = await _context.AgentSnapshots
            .FirstOrDefaultAsync(s => s.AgentId == agentId);

        if (snapshot is null)
            return new AgentSnapshotDto(agentId, MemorySummary: "", RecentTranscript: new());

        var transcript = snapshot.RecentTranscript
            .Select(t => (t.Role, t.Text))
            .ToList();

        return new AgentSnapshotDto(snapshot.AgentId, snapshot.MemorySummary, transcript);
    }

    [Activity]
    public async Task SaveSnapshotAsync(AgentSnapshotDto snapshotDto)
    {
        var existingSnapshot = await _context.AgentSnapshots
            .FirstOrDefaultAsync(s => s.AgentId == snapshotDto.AgentId);

        var transcriptEntries = snapshotDto.RecentTranscript
            .Select(t => new TranscriptEntry { Role = t.Role, Text = t.Text })
            .ToList();

        if (existingSnapshot is not null)
        {
            existingSnapshot.Update(snapshotDto.MemorySummary, transcriptEntries);
        }
        else
        {
            var newSnapshot = new AgentSnapshot(
                snapshotDto.AgentId,
                snapshotDto.MemorySummary,
                transcriptEntries);
            _context.AgentSnapshots.Add(newSnapshot);
        }

        await _context.SaveChangesAsync();
    }

    [Activity]
    public Task<NextStepDecision> DecideNextAsync(string userText, string memorySummary, List<ToolResult> toolResults)
    {
        userText ??= "";

        if (userText.Contains("clarify", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(new NextStepDecision(
                FinalAnswer: null,
                NeedUserQuestion: "What exactly do you want me to clarify (scope + desired output)?",
                ToolCalls: new()));

        if (toolResults.Count == 0 && (userText.Contains("research", StringComparison.OrdinalIgnoreCase) ||
                                       userText.Contains("find", StringComparison.OrdinalIgnoreCase)))
        {
            return Task.FromResult(new NextStepDecision(
                FinalAnswer: null,
                NeedUserQuestion: null,
                ToolCalls: new()
                {
                    new McpCall("mcp.search", "web_search", """{"query":"stub query"}"""),
                    new McpCall("mcp.docs", "lookup", """{"id":"stub"}"""),
                }));
        }

        var final = toolResults.Count > 0
            ? $"I did {toolResults.Count} tool call(s). Summary: {string.Join(" | ", toolResults.Select(t => t.Summary))}"
            : $"Got it. You said: {userText}";

        return Task.FromResult(new NextStepDecision(final, null, new()));
    }

    [Activity]
    public Task<ToolResult> CallMcpAsync(McpCall call)
    {
        var summary = $"[{call.Server}.{call.Tool}] args={call.JsonArgs}";
        return Task.FromResult(new ToolResult(summary, IsError: false));
    }

    [Activity]
    public Task NotifyUserAsync(Guid agentId, string message)
    {
        Console.WriteLine($"[NotifyUser] agent={agentId} message={message}");
        return Task.CompletedTask;
    }
}
