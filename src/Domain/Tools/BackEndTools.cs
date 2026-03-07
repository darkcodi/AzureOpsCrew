using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Domain.Tools.BackEnd.Planning;

namespace AzureOpsCrew.Domain.Tools;

public static class BackEndTools
{
    public static readonly IReadOnlyList<IBackendTool> All =
    [
        new GetMyIpTool(),
        new SkipTurnTool(),
        new WaitTool(),
        new ListTodoItemsTool(),
        new CreateTodoItemTool(),
        new MarkTodoItemCompletedTool(),
        new DeleteTodoItemTool(),
    ];
}
