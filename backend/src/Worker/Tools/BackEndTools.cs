using System.Text.Json;
using Worker.Models;

namespace Worker.Tools;

public static class BackEndTools
{
    public static List<ToolDeclaration> GetDeclarations()
    {
        return new List<ToolDeclaration>() { AddNumbersTool() };
    }

    private static ToolDeclaration AddNumbersTool()
    {
        JsonElement argsSchema = Schema("""
                                        {
                                          "type": "object",
                                          "properties": {
                                            "a": { "type": "number" },
                                            "b": { "type": "number" }
                                          },
                                          "required": ["a", "b"]
                                        }
                                        """);

        JsonElement returnSchema = Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "sum": { "type": "number" }
                                            },
                                            "required": ["sum"]
                                          }
                                          """);

        var pingTool = new ToolDeclaration
        {
            Name = "add_numbers",
            Description = "Adds two numbers and returns { sum }.",
            JsonSchema = argsSchema.ToString(),
            ReturnJsonSchema = returnSchema.ToString(),
            ToolType = ToolType.BackEnd,
        };

        return pingTool;
    }

    private static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
