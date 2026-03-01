using System.Text.Json;
using AzureOpsCrew.Infrastructure.Ai.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.Models;

public class ToolDeclaration
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JsonSchema { get; set; } = "{}";
    public string ReturnJsonSchema { get; set; } = "{}";
    public ToolType ToolType { get; set; } = ToolType.BackEnd;

    public AIFunctionDeclaration ToAiFunctionDeclaration()
    {
        return AIFunctionFactory.CreateDeclaration(
            name: Name,
            description: Description,
            jsonSchema: JsonElement.Parse(JsonSchema),
            returnJsonSchema: JsonElement.Parse(ReturnJsonSchema));
    }
}
