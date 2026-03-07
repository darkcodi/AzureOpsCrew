using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Domain.Tools.BackEnd;

public interface IBackendTool
{
    ToolDeclaration GetDeclaration();
    Task<ToolCallResult> ExecuteAsync(Agent agent, string callId, IDictionary<string, object?>? arguments);
}
