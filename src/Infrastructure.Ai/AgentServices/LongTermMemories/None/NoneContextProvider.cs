using Microsoft.Agents.AI;

namespace AzureOpsCrew.Infrastructure.Ai.AgentServices.LongTermMemories.None;

public class NoneContextProvider : AIContextProvider
{
    protected override async ValueTask<AIContext> InvokingCoreAsync(InvokingContext c, CancellationToken ct = default) => new();
}
