using System.Text.Json;
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
            case "addNumbers":
                return AddNumbers(call);
            default:
                return new ToolCallResult($"Unknown function: {call.Name}", IsError: true);
        }
    }

    private static ToolCallResult AddNumbers(AocFunctionCallContent call)
    {
        var args = call.Arguments ?? new Dictionary<string, object?>();
        if (!TryGetNumber(args, "a", out var a) || !TryGetNumber(args, "b", out var b))
        {
            return new ToolCallResult(
                JsonSerializer.Serialize(new { error = "Missing or invalid parameters: a and b must be numbers." }),
                IsError: true);
        }
        var sum = a + b;
        return new ToolCallResult(JsonSerializer.Serialize(new { sum }), IsError: false);
    }

    private static bool TryGetNumber(IDictionary<string, object?> args, string key, out double value)
    {
        value = 0;
        if (!args.TryGetValue(key, out var obj) || obj == null)
            return false;
        if (obj is JsonElement je)
        {
            if (je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var d))
            {
                value = d;
                return true;
            }
            return false;
        }
        if (obj is double dbl) { value = dbl; return true; }
        if (obj is int i) { value = i; return true; }
        if (obj is long l) { value = l; return true; }
        if (obj is decimal dec) { value = (double)dec; return true; }
        if (obj is float f) { value = f; return true; }
        if (double.TryParse(obj.ToString(), out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
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
