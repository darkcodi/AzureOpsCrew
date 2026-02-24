using System.Text.Json;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;
using Worker.Models;

namespace Worker.Activities;

public class AgentActivities
{
    private readonly AzureOpsCrewContext _context;
    private readonly IProviderFacadeResolver _providerFactory;

    public AgentActivities(AzureOpsCrewContext context, IProviderFacadeResolver providerFactory)
    {
        _context = context;
        _providerFactory = providerFactory;
    }

    [Activity]
    public async Task<Agent> LoadAgentAsync(Guid agentId)
    {
        var agent = await _context.Agents.FirstOrDefaultAsync(a => a.Id == agentId);
        if (agent is null)
            throw new Exception($"Agent not found: {agentId}");

        return agent;
    }

    [Activity]
    public async Task<Provider> LoadProviderAsync(Guid providerId)
    {
        var provider = await _context.Providers.FirstOrDefaultAsync(p => p.Id == providerId);
        if (provider is null)
            throw new Exception($"Provider not found: {providerId}");

        return provider;
    }

    [Activity]
    public async Task<AgentSnapshotDto> LoadSnapshotAsync(Guid agentId)
    {
        var snapshot = await _context.AgentSnapshots
            .FirstOrDefaultAsync(s => s.AgentId == agentId);

        if (snapshot is null)
            return new AgentSnapshotDto(agentId, MemorySummary: "", RecentTranscript: new());

        var transcript = snapshot.RecentTranscript
            .Select(t => (t.Role, t.Text))
            .ToList();

        return new AgentSnapshotDto(snapshot.AgentId, snapshot.MemorySummary, transcript);
    }

    [Activity]
    public async Task SaveSnapshotAsync(AgentSnapshotDto snapshotDto)
    {
        var existingSnapshot = await _context.AgentSnapshots
            .FirstOrDefaultAsync(s => s.AgentId == snapshotDto.AgentId);

        var transcriptEntries = snapshotDto.RecentTranscript
            .Select(t => new TranscriptEntry { Role = t.Role, Text = t.Text })
            .ToList();

        if (existingSnapshot is not null)
        {
            existingSnapshot.Update(snapshotDto.MemorySummary, transcriptEntries);
        }
        else
        {
            var newSnapshot = new AgentSnapshot(
                snapshotDto.AgentId,
                snapshotDto.MemorySummary,
                transcriptEntries);
            _context.AgentSnapshots.Add(newSnapshot);
        }

        await _context.SaveChangesAsync();
    }

    [Activity]
    public async Task<NextStepDecision> DecideNextAsync(
        Agent agent,
        Provider provider,
        string userText,
        string memorySummary,
        List<ToolResult> toolResults)
    {
        var providerService = _providerFactory.GetService(provider.ProviderType);
        var chatClient = providerService.CreateChatClient(provider, agent.Info.Model, CancellationToken.None);

        var fClient = new FunctionInvokingChatClient(chatClient);

        var chatMessages = new[]{new ChatMessage(ChatRole.User, userText)};
        // var chatOptions = new ChatOptions
        // {
        //     Tools = ...
        // };
        await foreach (var update in fClient.GetStreamingResponseAsync(chatMessages))
        {
            ActivityExecutionContext.Current.Logger.LogInformation("Received chat update: {Update}", JsonSerializer.Serialize(update));
        }

        return new NextStepDecision("Done", null, new());
    }

    [Activity]
    public Task<ToolResult> CallMcpAsync(McpCall call)
    {
        var summary = $"[{call.Server}.{call.Tool}] args={call.JsonArgs}";
        return Task.FromResult(new ToolResult(summary, IsError: false));
    }

    [Activity]
    public Task NotifyUserAsync(Guid agentId, string message)
    {
        Console.WriteLine($"[NotifyUser] agent={agentId} message={message}");
        return Task.CompletedTask;
    }
}
