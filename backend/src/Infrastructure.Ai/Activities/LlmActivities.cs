using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.Models;
using AzureOpsCrew.Infrastructure.Ai.Models.Content;
using AzureOpsCrew.Infrastructure.Ai.Tools;
using Microsoft.Extensions.AI;
using Temporalio.Activities;

namespace AzureOpsCrew.Infrastructure.Ai.Activities;

public class LlmActivities
{
    private readonly IProviderFacadeResolver _providerFactory;
    private readonly DatabaseActivities _databaseActivities;

    public const string HttpClientRole = "HTTP_CLIENT";

    public LlmActivities(IProviderFacadeResolver providerFactory, DatabaseActivities databaseActivities)
    {
        _providerFactory = providerFactory;
        _databaseActivities = databaseActivities;
    }

    [Activity]
    public async Task<List<AocAgentThought>> LlmThinkAsync(
        Agent agent,
        Provider provider,
        Guid threadId,
        Guid runId,
        List<AocAgentThought> messages,
        List<ToolDeclaration> tools)
    {
        var providerService = _providerFactory.GetService(provider.ProviderType);
        var chatClient = providerService.CreateChatClient(provider, agent.Info.Model, CancellationToken.None);
        var fClient = new FunctionInvokingChatClient(chatClient);

        var chatMessages = messages.Select(x => x.ToChatMessage()).ToList();

        const string SystemPrompt = """
You are attached to multiple group chats via tools.
You receive trigger messages from a SYSTEM when you receive a new message in any of the group chats you are attached to.
You can call tools to read the messages in the group chats, and respond back to the group chat.
When you respond back to the group chat, your message will be visible to all members of the group chat.
Your goal is to help the users in the group chat by providing useful information and performing actions on their behalf.
Always use the tools available to you when you need to interact with the group chat or perform actions.
NOTE: You should not respond to the SYSTEM messages directly, but use the tools to read the messages and respond back to the group chat. Your output is not visible to anyone. The only way to interact with the users is through the tools.
Once you have provided a response to the group chat via tools, output text "FINISHED". Just this one word and nothing else!
""";

        var beTools = string.Join("\n\n", tools.Where(x => x.ToolType == ToolType.BackEnd).Select(FormatToolDeclaration));
        var feTools = string.Join("\n\n", tools.Where(x => x.ToolType == ToolType.FrontEnd).Select(FormatToolDeclaration));

        if (string.IsNullOrEmpty(beTools))
        {
            beTools = "No backend tools available.";
        }
        if (string.IsNullOrEmpty(feTools))
        {
            feTools = "No frontend tools available.";
        }

        var prompt = $"""
System prompt:
{SystemPrompt}

Available backend tools:
{beTools}

Available frontend tools:
{feTools}

Your name is:
{agent.Info.Name}

Your description is:
{agent.Info.Description}

User prompt:
{agent.Info.Prompt}
""";
        var chatOptions = new ChatOptions
        {
            Instructions = prompt,
            Tools = tools.Select(x => (AITool)x.ToAiFunctionDeclaration()).ToArray(),
        };

        var newMessages = new List<AocAgentThought>();
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

                    var newMessage = AocAgentThought.FromContent(parsedContent, update.Role ?? ChatRole.Assistant, agent.Info.Name, now);
                    newMessages.Add(newMessage);
                }
            }
        }

        // Separate out messages related to HTTP client calls
        var httpClientMessages = newMessages.Where(m => m.Role.Value == HttpClientRole).ToArray();
        newMessages.RemoveAll(x => x.Role.Value == HttpClientRole);

        // We should have two messages for each http client call: one for the request and one for the response
        var httpRequestMessage = httpClientMessages.Length > 0 ? httpClientMessages[0] : null;
        var httpResponseMessage = httpClientMessages.Length > 1 ? httpClientMessages[1] : null;

        // Insert them in this activity to not pass huge strings between activities (they are stored in Temporal)
        var rawCall = new RawLlmHttpCall
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            ThreadId = threadId,
            RunId = runId,
            HttpRequest = (httpRequestMessage?.ContentDto?.ToAocAiContent() as AocTextContent)?.Text ?? "<empty>",
            HttpResponse = (httpResponseMessage?.ContentDto?.ToAocAiContent() as AocTextContent)?.Text ?? "<empty>",
            CreatedAt = DateTime.UtcNow,
        };
        await _databaseActivities.InsertRawLlmHttpCall(rawCall);

        ConcatTextContent(newMessages);

        return newMessages;
    }

    // If there are multiple text content in a row, we want to concat them into one content
    private static void ConcatTextContent(List<AocAgentThought> messages)
    {
        for (int i = messages.Count - 1; i > 0; i--)
        {
            var currentMessage = messages[i];
            var previousMessage = messages[i - 1];

            var currentContent = currentMessage.ContentDto.ToAocAiContent();
            var previousContent = previousMessage.ContentDto.ToAocAiContent();

            if (currentContent is AocTextContent currentTextContent && previousContent is AocTextContent previousTextContent &&
                currentMessage.Role == previousMessage.Role)
            {
                previousTextContent.Text += currentTextContent.Text;
                previousMessage.ContentDto = AocAiContentDto.FromAocAiContent(previousTextContent);
                messages.RemoveAt(i);
            }
            if (currentContent is AocTextReasoningContent currentReasoningContent && previousContent is AocTextReasoningContent previousReasoningContent &&
                currentMessage.Role == previousMessage.Role)
            {
                previousReasoningContent.Text += currentReasoningContent.Text;
                previousMessage.ContentDto = AocAiContentDto.FromAocAiContent(previousReasoningContent);
                messages.RemoveAt(i);
            }
        }
    }

    private static string FormatToolDeclaration(ToolDeclaration tool)
    {
        return $"""
Tool Name: {tool.Name}
Tool Description: {tool.Description}
Tool Type: {tool.ToolType.ToString()}
Tool JSON Schema: {tool.JsonSchema}
Tool Return JSON Schema: {tool.ReturnJsonSchema}
""";
    }
}
