using Temporalio.Activities;
using Worker.Models;

namespace Worker.Activities;

public class AgentActivities
{
    private static string DataDir => Path.Combine(AppContext.BaseDirectory, "data");

    [Activity]
    public async Task<AgentSnapshot> LoadSnapshotAsync(Guid agentId)
    {
        // Directory.CreateDirectory(DataDir);
        // var path = Path.Combine(DataDir, $"{agentId}.json");
        // if (!File.Exists(path))
        //     return new AgentSnapshot(agentId, MemorySummary: "", RecentTranscript: new());
        //
        // var json = await File.ReadAllTextAsync(path, ActivityExecutionContext.Current.CancellationToken);
        // return JsonSerializer.Deserialize<AgentSnapshot>(json)!;
        return new AgentSnapshot(agentId, MemorySummary: "", RecentTranscript: new());
    }

    [Activity]
    public async Task SaveSnapshotAsync(AgentSnapshot snapshot)
    {
        // Directory.CreateDirectory(DataDir);
        // var path = Path.Combine(DataDir, $"{snapshot.AgentId}.json");
        // var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        // await File.WriteAllTextAsync(path, json, ActivityExecutionContext.Current.CancellationToken);
    }

    [Activity]
    public Task<NextStepDecision> DecideNextAsync(string userText, string memorySummary, List<ToolResult> toolResults)
    {
        // This is deliberately dumb scaffolding. Replace with a real LLM call that:
        // - returns tool calls OR a question OR a final answer
        // - does NOT return chain-of-thought (store only short summaries if you want)
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
        // Replace with actual MCP request(s). Keep result SMALL; store large payloads elsewhere.
        var summary = $"[{call.Server}.{call.Tool}] args={call.JsonArgs}";
        return Task.FromResult(new ToolResult(summary, IsError: false));
    }

    [Activity]
    public Task NotifyUserAsync(Guid agentId, string message)
    {
        // Replace with email/push/slack/etc
        Console.WriteLine($"[NotifyUser] agent={agentId} message={message}");
        return Task.CompletedTask;
    }
}
