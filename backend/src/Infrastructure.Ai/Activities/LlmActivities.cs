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

        const string SystemPrompt = "You are one of agents in group chat: agents + human. When you have tools available, use them proactively to present information visually instead of plain text. Do NOT issue several tool calls in a row, and always wait for the result of a tool call before issuing another tool call. If you want to issue multiple tool calls, please issue them one by one and wait for the result of each tool call.";

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
