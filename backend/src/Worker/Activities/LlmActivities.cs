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

        var newMessages = new List<AocLlmChatMessage>();
        await foreach (ChatResponseUpdate update in fClient.GetStreamingResponseAsync(chatMessages, chatOptions))
        {
            var contents = update.Contents;
            foreach (var content in contents)
            {
                var parsedContent = AocAiContent.FromAiContent(content);
                if (parsedContent != null)
                {
                    // Ensure that messages are created in chronological order.
                    // This is important for the agent to process messages in the correct order, especially when there are tool calls involved.
                    await Task.Delay(TimeSpan.FromMilliseconds(1));
                    var now = DateTime.UtcNow;

                    var newMessage = AocLlmChatMessage.FromContent(parsedContent, ChatRole.Assistant, agent.Info.Name, now);
                    newMessages.Add(newMessage);
                }
            }
        }

        ConcatTextContent(newMessages);

        return newMessages;
    }

    // If there are multiple text content in a row, we want to concat them into one content
    private static void ConcatTextContent(List<AocLlmChatMessage> messages)
    {
        for (int i = messages.Count - 1; i > 0; i--)
        {
            var currentMessage = messages[i];
            var previousMessage = messages[i - 1];

            var currentContent = currentMessage.ContentDto.ToAocAiContent();
            var previousContent = previousMessage.ContentDto.ToAocAiContent();

            if (currentContent is AocTextContent currentTextContent && previousContent is AocTextContent previousTextContent)
            {
                previousTextContent.Text += currentTextContent.Text;
                previousMessage.ContentDto = AocAiContentDto.FromAocAiContent(previousTextContent);
                messages.RemoveAt(i);
            }
        }
    }
}
