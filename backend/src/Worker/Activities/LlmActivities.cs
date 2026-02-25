using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using Microsoft.Extensions.AI;
using Temporalio.Activities;
using Worker.Models;
using Worker.Models.Content;

namespace Worker.Activities;

public class LlmActivities
{
    private readonly IProviderFacadeResolver _providerFactory;

    public LlmActivities(IProviderFacadeResolver providerFactory)
    {
        _providerFactory = providerFactory;
    }

    [Activity]
    public async Task<List<AocLlmChatMessage>> LlmThinkAsync(
        Agent agent,
        Provider provider,
        List<AocLlmChatMessage> messages,
        List<ToolDeclaration> tools)
    {
        var providerService = _providerFactory.GetService(provider.ProviderType);
        var chatClient = providerService.CreateChatClient(provider, agent.Info.Model, CancellationToken.None);
        var fClient = new FunctionInvokingChatClient(chatClient);

        var chatMessages = messages.Select(x => x.ToChatMessage()).ToList();
        var chatOptions = new ChatOptions
        {
            Tools = tools.Select(x => (AITool)x.ToAiFunctionDeclaration()).ToArray(),
        };

        var contentList = new List<AocAiContent>();
        await foreach (ChatResponseUpdate update in fClient.GetStreamingResponseAsync(chatMessages, chatOptions))
        {
            var contents = update.Contents;
            foreach (var content in contents)
            {
                var parsed = AocAiContent.FromAiContent(content);
                if (parsed != null)
                {
                    contentList.Add(parsed);
                }
            }
        }

        ConcatTextContent(contentList);

        var newMessages = contentList.Select(x => AocLlmChatMessage.FromContent(x, ChatRole.Assistant)).ToList();

        return newMessages;
    }

    // If there are multiple text content in a row, we want to concat them into one content
    private static void ConcatTextContent(List<AocAiContent> messages)
    {
        for (int i = messages.Count - 1; i > 0; i--)
        {
            var current = messages[i];
            var previous = messages[i - 1];
            if (current is AocTextContent currentTextContent && previous is AocTextContent previousTextContent)
            {
                previousTextContent.Text += currentTextContent.Text;
                messages.RemoveAt(i);
            }
        }
    }
}
