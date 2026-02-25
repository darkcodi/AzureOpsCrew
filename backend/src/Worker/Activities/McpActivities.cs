using Temporalio.Activities;
using Worker.Models;
using Worker.Models.Content;

namespace Worker.Activities;

public class McpActivities
{
    [Activity]
    public Task<ToolCallResult> CallMcpAsync(AocFunctionCallContent call)
    {
        return Task.FromResult(new ToolCallResult("DONE", IsError: false));
    }
}
