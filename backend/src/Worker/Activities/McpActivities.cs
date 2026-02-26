using Temporalio.Activities;
using Worker.Models;
using Worker.Models.Content;

namespace Worker.Activities;

public class McpActivities
{

    [Activity]
    public async Task<ToolCallResult> CallMcpAsync(AocFunctionCallContent call)
    {
        // ToDo: Implement proper handling of MCP calls
        switch (call.Name)
        {
            case "getMyIp":
                return await GetMyIpAsync();
            default:
                return new ToolCallResult($"Unknown function: {call.Name}", IsError: true);
        }
    }

    private static readonly HttpClient HttpClient = new();
    private async Task<ToolCallResult> GetMyIpAsync()
    {
        var response = await HttpClient.GetAsync("https://free.freeipapi.com/api/json/");
        if (!response.IsSuccessStatusCode)
        {
            return new ToolCallResult($"Failed to call showMyIp API: {response.StatusCode}", IsError: true);
        }

        var content = await response.Content.ReadAsStringAsync();
        return new ToolCallResult(content, IsError: false);
    }
}
