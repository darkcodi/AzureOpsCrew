using System.Text.Json;
using Microsoft.Extensions.AI;
using Temporalio.Workflows;
using Worker.Activities;
using Worker.Models;
using Worker.Models.Content;

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
        var messages = new List<AocLlmChatMessage>()
        {
            new AocLlmChatMessage
            {
                Role = ChatRole.User,
                AuthorName = "User",
                CreatedAt = input.Trigger.CreatedAt,
                Content = new AocTextContent
                {
                    Text = userText,
                },
            },
        };

        // ToDo: Define a better stopping criteria. For example, we can let the agent decide when to stop by itself, or stop when reaching max context.
        const int maxSteps = 6;

        for (int step = 0; step < maxSteps; step++)
        {
            var newChatMessages = await Workflow.ExecuteActivityAsync(
                (LlmActivities a) => a.LlmThinkAsync(agent, provider, messages, tools),
                Options);
            messages.AddRange(newChatMessages);

            var domainMessages = newChatMessages.Select(m => m.ToDomain(agentId)).ToList();
            await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.BulkSaveLlmChatMessages(domainMessages), Options);

            var toolCalls = newChatMessages
                .Where(m => m.Content is AocFunctionCallContent)
                .Select(m => m.Content as AocFunctionCallContent)
                .Where(c => c != null)
                .Select(c => c!)
                .ToList();

            if (toolCalls.Any())
            {
                // ToDo: Add support for parallel tool calls if needed. For now we execute them sequentially for simplicity.
                foreach (var toolCall in toolCalls)
                {
                    var toolCallResult = await Workflow.ExecuteActivityAsync(
                        (McpActivities a) => a.CallMcpAsync(toolCall),
                        Options);
                    var toolCallResultMessage = new AocLlmChatMessage
                    {
                        Role = ChatRole.Tool,
                        CreatedAt = DateTime.UtcNow,
                        Content = new AocFunctionResultContent
                        {
                            CallId = toolCall.CallId,
                            Result = toolCallResult,
                        },
                    };
                    messages.Add(toolCallResultMessage);
                }
            }
            else
            {
                return new RunOutcome(RunOutcomeKind.Completed, null);
            }

        }

        return new RunOutcome(RunOutcomeKind.Completed, null);
    }

    private async Task<List<ToolDeclaration>> GetTools()
    {
        JsonElement argsSchema = Schema("""
                                        {
                                          "type": "object",
                                          "properties": {},
                                          "additionalProperties": false
                                        }
                                        """);

        JsonElement returnSchema = Schema("""{ "type": "string" }""");

        var pingTool = new ToolDeclaration
        {
            Name = "ping_something",
            Description = "This is a tool to ping something. It is used for testing purposes.",
            JsonSchema = argsSchema.ToString(),
            ReturnJsonSchema = returnSchema.ToString(),
        };

        return new List<ToolDeclaration> { pingTool };
    }

    private static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
