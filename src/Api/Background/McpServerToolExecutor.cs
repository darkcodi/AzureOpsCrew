using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Infrastructure.Ai.Mcp;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using Serilog;

namespace AzureOpsCrew.Api.Background;

public class McpServerToolExecutor
{
    private readonly McpServerFacade _mcpServerFacade;

    public McpServerToolExecutor(McpServerFacade mcpServerFacade)
    {
        _mcpServerFacade = mcpServerFacade;
    }

    public async Task<AocFunctionResultContent> ExecuteTool(
        AgentRunData agentRunData,
        ToolDeclaration toolDeclaration,
        AocFunctionCallContent toolCall)
    {
        if (toolDeclaration.McpServerConfigurationId is null)
        {
            Log.Warning("[MCP] Tool {ToolName} has ToolType.McpServer but no McpServerConfigurationId", toolDeclaration.Name);
            return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
        }

        var mcpServer = agentRunData.McpServers
            .FirstOrDefault(s => s.Id == toolDeclaration.McpServerConfigurationId.Value);

        if (mcpServer is null)
        {
            Log.Warning("[MCP] McpServerConfiguration {McpServerId} not found for tool {ToolName}",
                toolDeclaration.McpServerConfigurationId.Value, toolDeclaration.Name);
            return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
        }

        Log.Debug("[MCP] Calling tool {ToolName} on MCP server {ServerName} ({ServerUrl})",
            toolDeclaration.Name, mcpServer.Name, mcpServer.Url);

        var result = await _mcpServerFacade.CallToolAsync(
            mcpServer.Url,
            mcpServer.Auth,
            toolDeclaration.Name,
            toolCall.Arguments,
            CancellationToken.None);

        return new AocFunctionResultContent
        {
            CallId = toolCall.CallId,
            Result = new ToolCallResult(
                CallId: toolCall.CallId,
                Result: result.Content,
                IsError: result.IsError),
        };
    }
}
