using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using Microsoft.Extensions.AI;
using Temporalio.Activities;

namespace Worker.Activities;

public class LlmActivities
{
    private readonly IProviderFacadeResolver _providerFactory;

    public LlmActivities(IProviderFacadeResolver providerFactory)
    {
        _providerFactory = providerFactory;
    }

    [Activity]
    public async Task<List<ChatMessage>> LlmThinkAsync(
        Agent agent,
        Provider provider,
        List<ChatMessage> messages,
        List<AIFunctionDeclaration> tools)
    {
        var providerService = _providerFactory.GetService(provider.ProviderType);
        var chatClient = providerService.CreateChatClient(provider, agent.Info.Model, CancellationToken.None);

        var fClient = new FunctionInvokingChatClient(chatClient);

        var chatOptions = new ChatOptions
        {
            Tools = tools.Select(x => (AITool)x).ToArray(),
        };

        var newMessages = new List<ChatMessage>();
        await foreach (ChatResponseUpdate update in fClient.GetStreamingResponseAsync(messages, chatOptions))
        {
            var chatMessage = ToChatMessage(update);
            newMessages.Add(chatMessage);
        }

        return newMessages;
    }

    private static ChatMessage ToChatMessage(ChatResponseUpdate update)
    {
        return new ChatMessage(update.Role ?? ChatRole.Assistant, update.Contents);
    }
}
