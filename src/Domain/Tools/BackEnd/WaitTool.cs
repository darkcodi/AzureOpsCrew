using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd;

public class WaitTool : ITool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "wait",
            Description = "Blocks an execution loop for a specified duration before taking the next action. Useful for scenarios where the agent needs to pause, and wait for something. Max duration is 300 seconds (5 minutes).",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "durationSeconds": { "type": "number", "description": "The integer number of seconds to wait" }
                                            },
                                            "required": ["durationSeconds"],
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                    "type": "object",
                                                    "properties": { },
                                                    "required": [],
                                                    "additionalProperties": false
                                                }
                                                """).ToString(),
            ToolType = ToolType.BackEnd,
        };
    }

    public async Task<ToolCallResult> ExecuteAsync(Agent agent, string callId, IDictionary<string, object?>? arguments)
    {
        // validate arguments
        if (arguments == null || !arguments.ContainsKey("durationSeconds"))
        {
            return new ToolCallResult(callId, new { ErrorMessage = "durationSeconds param is missing" }, true);
        }
        if (!int.TryParse(arguments["durationSeconds"]?.ToString(), out var durationSeconds))
        {
            return new ToolCallResult(callId, new { ErrorMessage = "durationSeconds param is not a valid integer number" }, true);
        }
        if (durationSeconds < 0 || durationSeconds > 300)
        {
            return new ToolCallResult(callId, new { ErrorMessage = "durationSeconds param must be between 0 and 300" }, true);
        }

        // wait for the specified duration
        await Task.Delay(TimeSpan.FromSeconds(durationSeconds));

        // return success
        return new ToolCallResult(callId, null, false);
    }
}
