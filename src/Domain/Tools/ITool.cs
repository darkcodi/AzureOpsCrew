using AzureOpsCrew.Domain.Agents;

namespace AzureOpsCrew.Domain.Tools;

public interface ITool
{
    ToolDeclaration GetDeclaration();
    Task<ToolCallResult> ExecuteAsync(Agent agent, string callId, IDictionary<string, object?>? arguments);
}
