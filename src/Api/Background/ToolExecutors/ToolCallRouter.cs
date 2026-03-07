using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using Serilog;

namespace AzureOpsCrew.Api.Background.ToolExecutors;

public class ToolCallRouter
{
    private readonly BackendToolExecutor _backendToolExecutor;
    private readonly McpServerToolExecutor _mcpServerToolExecutor;

    public ToolCallRouter(BackendToolExecutor backendToolExecutor, McpServerToolExecutor mcpServerToolExecutor)
    {
        _backendToolExecutor = backendToolExecutor;
        _mcpServerToolExecutor = mcpServerToolExecutor;
    }

    public async Task<AocFunctionResultContent> ExecuteToolCall(AocFunctionCallContent toolCall, AgentRunData data)
    {
        var toolName = toolCall.Name;
        var toolDeclaration = data.Tools.FirstOrDefault(t => string.Equals(t.Name, toolName, StringComparison.OrdinalIgnoreCase));
        if (toolDeclaration is null)
        {
            // If the tool declaration is not found, we return an error result for this tool call.
            // This can happen if the LLM calls a tool that is not declared in the prompt or if there is a typo in the tool name.
            Log.Warning("[BACKGROUND] Tool {ToolName} not found in declarations", toolName);

            return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
        }

        try
        {
            if (toolDeclaration.ToolType == ToolType.FrontEnd)
            {
                Log.Debug("[BACKGROUND] Front-end tool {ToolName} called, returning empty result", toolName);

                // For front-end tools, we can return an empty result immediately since the front-end will handle the rendering based on the tool declaration.
                return AocFunctionResultContent.Empty(toolCall.CallId);
            }
            if (toolDeclaration.ToolType == ToolType.BackEnd)
            {
                Log.Debug("[BACKGROUND] Executing tool {ToolName} (type: {ToolType})", toolName, toolDeclaration.ToolType);

                return await _backendToolExecutor.ExecuteTool(data.Agent, toolDeclaration, toolCall);
            }
            if (toolDeclaration.ToolType == ToolType.McpServer)
            {
                Log.Debug("[BACKGROUND] Executing tool {ToolName} (type: {ToolType})", toolName, toolDeclaration.ToolType);

                return await _mcpServerToolExecutor.ExecuteTool(data, toolDeclaration, toolCall);
            }

            return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[BACKGROUND] Error executing tool {ToolName}", toolName);

            // Return an error result similar to ToolDoesNotExist pattern
            return new AocFunctionResultContent
            {
                CallId = toolCall.CallId,
                Result = new ToolCallResult(CallId: toolCall.CallId, Result: new { ErrorMessage = ex.Message }, IsError: true),
            };
        }
    }
}
