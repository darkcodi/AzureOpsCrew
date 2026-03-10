namespace AzureOpsCrew.Domain.Tools.BackEnd;

public static class BackEndTools
{
    public static readonly IReadOnlyList<IBackendTool> All =
    [
        new GetMyIpTool(),
        new SkipTurnTool(),
        new WaitTool(),
    ];
}
