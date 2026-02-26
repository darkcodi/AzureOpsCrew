using System.Text.Json;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Utils;
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

        var domainMessages = await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.LoadChatHistory(agentId), Options);
        domainMessages = domainMessages.Where(m => !m.IsHidden).ToList();
        var messages = domainMessages.Select(AocLlmChatMessage.FromDomain).ToList();

        // ToDo: Load tools based on agent configuration. For now we just return a hardcoded tool list for testing.
        var tools = await GetTools();

        // ToDo: Define a better stopping criteria. For example, we can let the agent decide when to stop by itself, or stop when reaching max context.
        const int maxSteps = 6;

        for (int step = 0; step < maxSteps; step++)
        {
            var newChatMessages = await Workflow.ExecuteActivityAsync(
                (LlmActivities a) => a.LlmThinkAsync(agent, provider, messages, tools),
                Options);

            // Ensure new messages are in chronological order (in case the LLM returns them out of order)
            newChatMessages = newChatMessages.OrderBy(x => x.CreatedAt).ToList();

            messages.AddRange(newChatMessages);

            var newDomainMessages = newChatMessages.Select(m => m.ToDomain(agentId, input.ThreadId, input.RunId)).ToList();
            foreach (var newDomainMessage in newDomainMessages)
            {
                await Workflow.ExecuteActivityAsync((DatabaseActivities a) => a.UpsertLlmChatMessage(newDomainMessage), Options);
            }

            var toolCalls = newChatMessages
                .Select(m => m.ContentDto.ToAocAiContent())
                .OfType<AocFunctionCallContent>()
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
                        CreatedAt = Workflow.UtcNow,
                        ContentDto = AocAiContentDto.FromAocAiContent(new AocFunctionResultContent
                        {
                            CallId = toolCall.CallId,
                            Result = toolCallResult,
                        }),
                    };
                    messages.Add(toolCallResultMessage);
                }
            }
            else
            {
                break;
            }

        }

        return new RunOutcome(RunOutcomeKind.Completed, null);
    }

    private async Task<List<ToolDeclaration>> GetTools()
    {
        JsonElement argsSchema = Schema("""
                                        {
                                          "type": "object",
                                          "properties": {
                                            "a": { "type": "number" },
                                            "b": { "type": "number" }
                                          },
                                          "required": ["a", "b"]
                                        }
                                        """);

        JsonElement returnSchema = Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "sum": { "type": "number" }
                                            },
                                            "required": ["sum"]
                                          }
                                          """);

        var pingTool = new ToolDeclaration
        {
            Name = "add_numbers",
            Description = "Adds two numbers and returns { sum }.",
            JsonSchema = argsSchema.ToString(),
            ReturnJsonSchema = returnSchema.ToString(),
        };

        return new List<ToolDeclaration> { pingTool };
    }

    private static JsonElement Schema(string json)
        => JsonDocument.Parse(json).RootElement.Clone();
}
