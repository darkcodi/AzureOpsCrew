using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;

namespace AzureOpsCrew.Api.Background;

public class ToolExecutor
{
    public async Task<AocFunctionResultContent> ExecuteTool(
        Agent agent,
        ToolDeclaration toolDeclaration,
        AocFunctionCallContent toolCall)
    {
        var tool = BackEndTools.All
            .FirstOrDefault(t => t.GetDeclaration().Name == toolDeclaration.Name);

        if (tool == null)
        {
            return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
        }

        var result = await tool.ExecuteAsync(agent, toolCall.CallId, toolCall.Arguments ?? new Dictionary<string, object?>());

        return new AocFunctionResultContent
        {
            CallId = toolCall.CallId,
            Result = result,
        };
    }
}
