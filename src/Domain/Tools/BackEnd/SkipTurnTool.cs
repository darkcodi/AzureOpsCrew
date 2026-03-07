using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Domain.Tools.BackEnd;

public class SkipTurnTool : IBackendTool
{
    public static string ToolName => "skipTurn";

    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = ToolName,
            Description = "Skips the agent's current turn without performing any action. Useful when the agent determines that it cannot or should not take any action during this turn. ",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "reason": { "type": "string", "description": "The reason for skipping the turn" }
                                            },
                                            "required": ["reason"],
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

    public Task<ToolCallResult> ExecuteAsync(Agent agent, string callId, IDictionary<string, object?>? arguments)
    {
        return Task.FromResult(new ToolCallResult(callId, null, false));
    }
}
