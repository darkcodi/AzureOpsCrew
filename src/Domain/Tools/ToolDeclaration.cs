using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.Tools;

public class ToolDeclaration
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JsonSchema { get; set; } = "{}";
    public string ReturnJsonSchema { get; set; } = "{}";
    public ToolType ToolType { get; set; } = ToolType.BackEnd;

    // Only set for ToolType.McpServer tools. References the McpServerConfiguration that owns this tool.
    public Guid? McpServerConfigurationId { get; set; }

    public AIFunctionDeclaration ToAiFunctionDeclaration()
    {
        return AIFunctionFactory.CreateDeclaration(
            name: Name,
            description: Description,
            jsonSchema: JsonElement.Parse(JsonSchema),
            returnJsonSchema: JsonElement.Parse(ReturnJsonSchema));
    }

    public string FormatToolDeclaration()
    {
        return $"""
Tool Name: {Name}
Tool Description: {Description}
Tool Type: {ToolType.ToString()}
Tool JSON Schema: {JsonSchema}
Tool Return JSON Schema: {ReturnJsonSchema}
""";
    }
}
