using AzureOpsCrew.Api.Endpoints.Dtos.ChatHistory;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Worker.Models.Content;

namespace AzureOpsCrew.Api.Endpoints;

public static class ChatHistoryEndpoints
{
    public static void MapChatHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat-history")
            .WithTags("Chat History")
            .RequireAuthorization();

        group.MapGet("/agents/{agentId:guid}", async (
            Guid agentId,
            AzureOpsCrewContext context,
            CancellationToken cancellationToken) =>
        {
            var messages = await context.LlmChatMessages
                .Where(m => m.AgentId == agentId && !m.IsHidden)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(cancellationToken);

            var historyMessages = new List<ChatHistoryMessage>();

            // First pass: collect tool result content by CallId (from tool-role messages)
            var toolResultsByCallId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var msg in messages)
            {
                if (msg.Role.ToString() != "tool")
                    continue;
                var contentDto = new AocAiContentDto
                {
                    Content = msg.ContentJson,
                    ContentType = msg.ContentType
                };
                var aiContent = contentDto.ToAocAiContent();
                if (aiContent is AocFunctionResultContent functionResult)
                {
                    var resultStr = functionResult.Result switch
                    {
                        null => "<null>",
                        string s => s,
                        JsonElement el => el.GetRawText(),
                        _ => JsonSerializer.Serialize(functionResult.Result)
                    };
                    toolResultsByCallId[functionResult.CallId] = resultStr ?? "";
                }
            }

            foreach (var msg in messages)
            {
                // Skip tool role messages (internal function calls)
                if (msg.Role.ToString() == "tool")
                    continue;

                // Deserialize content
                var contentDto = new AocAiContentDto
                {
                    Content = msg.ContentJson,
                    ContentType = msg.ContentType
                };
                var aiContent = contentDto.ToAocAiContent();

                if (aiContent is AocTextContent textContent)
                {
                    historyMessages.Add(new ChatHistoryMessage
                    {
                        Id = msg.Id.ToString(),
                        Role = msg.Role.ToString() == "user" ? "user" : "assistant",
                        Content = textContent.Text,
                        Timestamp = msg.CreatedAt
                    });
                }
                else if (aiContent is AocTextReasoningContent reasoningContent)
                {
                    historyMessages.Add(new ChatHistoryMessage
                    {
                        Id = msg.Id.ToString(),
                        Role = msg.Role.ToString() == "user" ? "user" : "assistant",
                        Content = null,
                        Reasoning = reasoningContent.Text,
                        Timestamp = msg.CreatedAt
                    });
                }
                else if (aiContent is AocFunctionCallContent functionCallContent
                    && toolResultsByCallId.TryGetValue(functionCallContent.CallId, out var resultStr))
                {
                    object? resultObj;
                    try
                    {
                        resultObj = JsonSerializer.Deserialize<JsonElement>(resultStr);
                    }
                    catch
                    {
                        resultObj = new Dictionary<string, object?> { ["raw"] = resultStr };
                    }

                    historyMessages.Add(new ChatHistoryMessage
                    {
                        Id = functionCallContent.CallId,
                        Role = "assistant",
                        Content = "",
                        Timestamp = msg.CreatedAt,
                        Widget = new ChatHistoryWidget
                        {
                            ToolName = functionCallContent.Name,
                            CallId = functionCallContent.CallId,
                            Args = functionCallContent.Arguments ?? new Dictionary<string, object?>(),
                            Result = resultObj
                        }
                    });
                }
            }

            return Results.Ok(new ChatHistoryResponse { Messages = historyMessages });
        })
        .Produces<ChatHistoryResponse>(StatusCodes.Status200OK);
    }
}
