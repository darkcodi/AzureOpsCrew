using AzureOpsCrew.Api.Background.Tools;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;

namespace AzureOpsCrew.Api.Background.ToolExecutors;

public class BackendToolExecutor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyList<IBackendTool> _allTools;

    public BackendToolExecutor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _allTools = BackEndTools.All
            .Append(new GetMessagesTool())
            .Append(new PostMessageTool())
            .ToList();
    }

    public IReadOnlyList<IBackendTool> AllTools => _allTools;

    public async Task<AocFunctionResultContent> ExecuteTool(
        AgentRunData data,
        ToolDeclaration toolDeclaration,
        AocFunctionCallContent toolCall)
    {
        var tool = _allTools
            .FirstOrDefault(t => t.GetDeclaration().Name == toolDeclaration.Name);

        if (tool == null)
        {
            return AocFunctionResultContent.ToolDoesNotExist(toolCall.CallId);
        }

        var result = await tool.ExecuteAsync(data, toolCall.CallId, toolCall.Arguments ?? new Dictionary<string, object?>(), _serviceProvider);

        return new AocFunctionResultContent
        {
            CallId = toolCall.CallId,
            Result = result,
        };
    }
}
