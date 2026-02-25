using Serilog;
using Temporalio.Activities;
using Worker.Models;
using Worker.Models.Content;

namespace Worker.Activities;

public class McpActivities
{
    [Activity]
    public Task<ToolResult> CallMcpAsync(AocFunctionCallContent call)
    {
        return Task.FromResult(new ToolResult("DONE", IsError: false));
    }

    [Activity]
    public Task NotifyUserAsync(Guid agentId, string message)
    {
        Log.Information($"[NotifyUser] agent={agentId} message={message}");
        return Task.CompletedTask;
    }
}
