using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Domain.Tools.BackEnd;

public interface IBackendTool
{
    ToolDeclaration GetDeclaration();
    Task<ToolCallResult> ExecuteAsync(AgentRunData data, string callId, IDictionary<string, object?>? arguments, IServiceProvider serviceProvider);
}
