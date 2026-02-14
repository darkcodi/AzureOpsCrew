using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

public class AGUITool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("parameters")]
    public JsonElement Parameters { get; set; }
}

public static class AIToolExtensions
{
    public static IEnumerable<AGUITool> AsAGUITools(this IEnumerable<AITool> tools)
    {
        if (tools is null)
        {
            yield break;
        }

        foreach (var tool in tools)
        {
            // Convert both AIFunctionDeclaration and AIFunction (which extends it) to AGUITool
            // For AIFunction, we send only the metadata (Name, Description, JsonSchema)
            // The actual executable implementation stays on the client side
            if (tool is AIFunctionDeclaration function)
            {
                yield return new AGUITool
                {
                    Name = function.Name,
                    Description = function.Description,
                    Parameters = function.JsonSchema
                };
            }
        }
    }

    public static IEnumerable<AITool> AsAITools(this IEnumerable<AGUITool> tools)
    {
        if (tools is null)
        {
            yield break;
        }

        foreach (var tool in tools)
        {
            // Create a function declaration from the AG-UI tool definition
            // Note: These are declaration-only and cannot be invoked, as the actual
            // implementation exists on the client side
            yield return AIFunctionFactory.CreateDeclaration(
                name: tool.Name,
                description: tool.Description,
                jsonSchema: tool.Parameters);
        }
    }
}
