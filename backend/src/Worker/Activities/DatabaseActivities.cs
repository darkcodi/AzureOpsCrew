using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Temporalio.Activities;

namespace Worker.Activities;

public class DatabaseActivities
{
    private readonly AzureOpsCrewContext _context;

    public DatabaseActivities(AzureOpsCrewContext context)
    {
        _context = context;
    }

    [Activity]
    public async Task<Agent> LoadAgent(Guid agentId)
    {
        var agent = await _context.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
        if (agent is null)
            throw new Exception($"Agent not found: {agentId}");

        return agent;
    }

    [Activity]
    public async Task<Provider> LoadProvider(Guid providerId)
    {
        var provider = await _context.Providers.FirstOrDefaultAsync(p => p.Id == providerId);
        if (provider is null)
            throw new Exception($"Provider not found: {providerId}");

        return provider;
    }
}
