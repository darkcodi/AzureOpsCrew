using System.Text.Json;
using AzureOpsCrew.Domain.LLMOutputs;
using Microsoft.Extensions.AI;
using Temporalio.Workflows;
using Worker.Activities;
using Worker.Models;

namespace Worker.Workflows;

[Workflow]
public class AgentRunWorkflow
{
    private static readonly ActivityOptions Options = new()
    {
        StartToCloseTimeout = TimeSpan.FromMinutes(2),
        RetryPolicy = new() { MaximumAttempts = 3 }
    };

    [WorkflowRun]
    public async Task<RunOutcome> RunAsync(RunInput input)
    {
        var agentId = input.AgentId;

        var agent = await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.LoadAgent(agentId), Options);
        var provider = await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.LoadProvider(agent.ProviderId), Options);
        var tools = await GetTools();

        var userText = input.Trigger.Text ?? "";

        // ToDo: Add memory loading
        var messages = new List<ChatMessage>() { new ChatMessage(ChatRole.User, userText) };

        const int maxSteps = 6;

        for (int step = 0; step < maxSteps; step++)
        {
            var newChatMessages = await Workflow.ExecuteActivityAsync(
                (LlmActivities a) => a.LlmThinkAsync(agent, provider, messages, tools),
                Options);
            newChatMessages = ConcatMessages(newChatMessages);

            var llmOutputs = ToLllmOutputs(input, newChatMessages);
            await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.SaveLlmOutputBulk(llmOutputs), Options);

            // foreach (var llmOutput in newChatMessages)
            // {
            //     var res = await Workflow.ExecuteActivityAsync(
            //         (McpActivities a) => a.CallMcpAsync(call),
            //         Options);
            //     toolResults.Add(res);
            // }
            //
            // if (decision.FinalAnswer is not null)
            // {
            //     return new RunOutcome(RunOutcomeKind.Completed, decision.FinalAnswer, null);
            // }
        }

        return new RunOutcome(
            RunOutcomeKind.Completed,
            new FinalAnswer("I hit my step budget. Tell me what to focus on next.", null), null);
    }

    private static List<ChatMessage> ConcatMessages(List<ChatMessage> messages)
    {
        var chatMessages = new List<ChatMessage>();
        var lastChatMessage = messages.FirstOrDefault();
        if (lastChatMessage == null)
        {
            return chatMessages;
        }

        // Concat adjusent TextContent into one ChatMessage, so that we can have a better display in UI and also better token usage.
        foreach (var message in messages)
        {
            if (message.Role == ChatRole.Assistant)
            {
                var text = string.Join("", message.Contents.OfType<TextContent>().Select(c => c.Text));
                chatMessages.Add(new ChatMessage(message.Role, text));
            }
            else
            {
                chatMessages.Add(message);
            }
        }

        return chatMessages;
    }

    private static List<LlmOutput> ToLllmOutputs(RunInput input, IList<ChatMessage> chatMessages)
    {
        var outputs = new List<LlmOutput>();

        foreach (var chatMessage in chatMessages)
        {
            var messageContents = chatMessage.Contents;
            var functionCalls = messageContents.OfType<FunctionCallContent>().ToList();

            if (functionCalls.Any())
            {
                foreach (var functionCall in functionCalls)
                {
                    outputs.Add(new LlmOutput(Guid.NewGuid(), input.RunId, chatMessage.Text, functionCall.Name, null, null));
                }
            }
            else
            {
                outputs.Add(new LlmOutput(Guid.NewGuid(), input.RunId, chatMessage.Text, null, null, null));
            }
        }

        return outputs;
    }

    private async Task<List<AIFunctionDeclaration>> GetTools()
    {
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

        return new List<AIFunctionDeclaration> { getStatus };
    }

    private static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
