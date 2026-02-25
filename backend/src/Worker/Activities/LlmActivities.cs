using System.Text.Json;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
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
    public async Task<NextStepDecision> LlmThinkAsync(
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

        return ToLlmOutput(contentList);
    }

    private static NextStepDecision ToLlmOutput(List<AocAiContent> contentList)
    {
        // extract usage stats
        var usageContents = contentList.OfType<AocUsageContent>().ToArray();
        contentList.RemoveAll(x => x is AocUsageContent);
        var lastUsageContent = usageContents.LastOrDefault();

        // extract text response
        var textResponse = string.Join(string.Empty, contentList.OfType<AocTextContent>().Select(c => c.Text));
        contentList.RemoveAll(x => x is AocTextContent);

        // extract tool calls
        var toolCalls = contentList.OfType<AocFunctionCallContent>().ToList();
        contentList.RemoveAll(x => x is AocFunctionCallContent);

        if (contentList.Any())
        {
            ActivityExecutionContext.Current.Logger.LogWarning("There are unprocessed content after parsing: {ContentTypes}", string.Join(",", contentList.Select(c => c.GetType().Name)));
        }

        if (toolCalls.Any())
        {
            return new NextStepDecision(null, new ToolCallsRequest(textResponse, toolCalls));
        }

        return new NextStepDecision(new FinalAnswer(textResponse, lastUsageContent), null);
    }

    static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
