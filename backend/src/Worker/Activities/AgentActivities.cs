using System.Text.Json;
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

        // No args: empty object, no extra properties allowed.
        JsonElement argsSchema = Schema("""
        {
          "type": "object",
          "properties": {},
          "additionalProperties": false
        }
        """);

        JsonElement returnSchema = Schema("""{ "type": "string" }""");

        AIFunctionDeclaration getStatus =
            AIFunctionFactory.CreateDeclaration(
                name: "get_info_about_me",
                description: "Get info about the agent itself, such as its capabilities, tools, etc. This can help the agent to better utilize itself.",
                jsonSchema: argsSchema,
                returnJsonSchema: returnSchema);

        var chatOptions = new ChatOptions
        {
            Tools = new List<AITool>()
            {
                getStatus,
            },
        };

        var contentList = new List<AocAiContent>();
        await foreach (ChatResponseUpdate update in fClient.GetStreamingResponseAsync(chatMessages, chatOptions))
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

        var usageContents = contentList.OfType<AocUsageContent>().ToArray();
        contentList.RemoveAll(x => x is AocUsageContent);
        var lastUsageContent = usageContents.LastOrDefault();

        var toolCalls = contentList.OfType<AocFunctionCallContent>().ToList();
        if (toolCalls.Any())
        {
            return new NextStepDecision(null, null, toolCalls);
        }

        if (contentList.FirstOrDefault() is AocTextContent)
        {
            // For simplicity, if the first content is text, treat it as final answer. In real scenario, should have a more robust way to determine this.
            var textResponse = string.Join(string.Empty, contentList.OfType<AocTextContent>().Select(c => c.Text));
            var finalAnswer = new FinalAnswer
            {
                Text = textResponse,
            };
            if (lastUsageContent != null)
            {
                finalAnswer.Usage = lastUsageContent;
            }
            return new NextStepDecision(finalAnswer, null, new());
        }
        else
        {
            return new NextStepDecision(new FinalAnswer { Text = "TODO#47" }, null, new());
        }
    }

    [Activity]
    public Task<ToolResult> CallMcpAsync(AocFunctionCallContent call)
    {
        return Task.FromResult(new ToolResult("DONE", IsError: false));
    }

    [Activity]
    public Task NotifyUserAsync(Guid agentId, string message)
    {
        Log.Information($"[NotifyUser] agent={agentId} message={message}");
        return Task.CompletedTask;
    }

    static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
