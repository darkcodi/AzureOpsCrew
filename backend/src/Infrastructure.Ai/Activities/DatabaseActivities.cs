using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Activities;

namespace AzureOpsCrew.Infrastructure.Ai.Activities;

public class DatabaseActivities
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseActivities(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    [Activity]
    public async Task<Agent> LoadAgent(Guid agentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();

        var agent = await context.Agents
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agentId);
        if (agent is null)
            throw new Exception($"Agent not found: {agentId}");

        return agent;
    }

    [Activity]
    public async Task<Provider> LoadProvider(Guid providerId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();

        var provider = await context.Providers
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == providerId);
        if (provider is null)
            throw new Exception($"Provider not found: {providerId}");

        return provider;
    }

    [Activity]
    public async Task<List<LlmChatMessage>> LoadChatHistory(Guid agentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();

        var messages = await context.LlmChatMessages
            .AsNoTracking()
            .Where(m => m.AgentId == agentId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return messages;
    }

    [Activity]
    public async Task UpsertLlmChatMessage(LlmChatMessage chatMessage)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();

        // Query database to determine if entity exists
        var existingMessage = await context.LlmChatMessages
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == chatMessage.Id);

        if (existingMessage is null)
        {
            await context.LlmChatMessages.AddAsync(chatMessage);
        }
        else
        {
            context.LlmChatMessages.Update(chatMessage);
        }

        await context.SaveChangesAsync();
    }

    [Activity]
    public async Task InsertRawLlmHttpCall(RawLlmHttpCall rawCall)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();

        await context.RawLlmHttpCalls.AddAsync(rawCall);
        await context.SaveChangesAsync();
    }
}
