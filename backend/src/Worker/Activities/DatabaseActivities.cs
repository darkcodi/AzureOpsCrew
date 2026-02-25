using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
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

    [Activity]
    public async Task<List<LlmChatMessage>> LoadChatHistory(Guid agentId)
    {
        var messages = await _context.LlmChatMessages
            .Where(m => m.AgentId == agentId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();
        return messages;
    }

    [Activity]
    public async Task UpsertLlmChatMessage(LlmChatMessage chatMessage)
    {
        var existingMessage = await _context.LlmChatMessages
            .FirstOrDefaultAsync(m => m.Id == chatMessage.Id);

        if (existingMessage is null)
        {
            await _context.LlmChatMessages.AddAsync(chatMessage);
        }
        else
        {
            existingMessage.AgentId = chatMessage.AgentId;
            existingMessage.RunId = chatMessage.RunId;
            existingMessage.Role = chatMessage.Role;
            existingMessage.AuthorName = chatMessage.AuthorName;
            existingMessage.ContentJson = chatMessage.ContentJson;
            existingMessage.IsHidden = chatMessage.IsHidden;
            existingMessage.CreatedAt = chatMessage.CreatedAt;

            _context.LlmChatMessages.Update(existingMessage);
        }

        await _context.SaveChangesAsync();
    }
}
