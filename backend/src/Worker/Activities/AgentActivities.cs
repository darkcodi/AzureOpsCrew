using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Serilog;
using Temporalio.Activities;
using Worker.Models;
using Worker.Models.Content;

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
    public async Task<NextStepDecision> AgentThinkAsync(
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

        var contentList = new List<AocAiContent>();
        await foreach (ChatResponseUpdate update in fClient.GetStreamingResponseAsync(chatMessages))
        {
            var contents = update.Contents;
            foreach (var content in contents)
            {
                var parsed = AocAiContent.Parse(content);
                if (parsed != null)
                {
                    contentList.Add(parsed);
                }
            }
        }

        if (contentList.FirstOrDefault() is AocTextContent)
        {
            // For simplicity, if the first content is text, treat it as final answer. In real scenario, should have a more robust way to determine this.
            var textResponse = string.Join(string.Empty, contentList.OfType<AocTextContent>().Select(c => c.Text));
            var finalAnswer = new FinalAnswer
            {
                Text = textResponse,
            };
            var stats = contentList.OfType<AocUsageContent>().FirstOrDefault();
            if (stats != null)
            {
                finalAnswer.InputTokenCount = stats.InputTokenCount;
                finalAnswer.OutputTokenCount = stats.OutputTokenCount;
                finalAnswer.TotalTokenCount = stats.TotalTokenCount;
                finalAnswer.CachedInputTokenCount = stats.CachedInputTokenCount;
                finalAnswer.ReasoningTokenCount = stats.ReasoningTokenCount;
            }
            return new NextStepDecision(finalAnswer, null, new());
        }
        else
        {
            return new NextStepDecision(new FinalAnswer { Text = "TODO#47" }, null, new());
        }
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
        Log.Information($"[NotifyUser] agent={agentId} message={message}");
        return Task.CompletedTask;
    }
}
