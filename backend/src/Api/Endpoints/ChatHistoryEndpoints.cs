using AzureOpsCrew.Api.Endpoints.Dtos.ChatHistory;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Worker.Models.Content;
using Worker.Tools;

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
                else if (aiContent is AocFunctionCallContent functionCallContent
                    && FrontEndTools.IsFrontEndTool(functionCallContent.Name)
                    && functionCallContent.Name.Equals("showMyIp", StringComparison.OrdinalIgnoreCase)
                    && functionCallContent.Arguments != null)
                {
                    // Return showMyIp tool call as a widget-only message so the frontend can restore the IP card
                    historyMessages.Add(new ChatHistoryMessage
                    {
                        Id = functionCallContent.CallId,
                        Role = "assistant",
                        Content = "",
                        Timestamp = msg.CreatedAt,
                        Widget = new ChatHistoryWidget { ToolName = functionCallContent.Name, Data = functionCallContent.Arguments }
                    });
                }
                else if (aiContent is AocFunctionCallContent functionCallContentDeploy
                    && functionCallContentDeploy.Name.Equals("showDeployment", StringComparison.OrdinalIgnoreCase))
                {
                    // Return showDeployment tool call as a widget-only message (no args)
                    historyMessages.Add(new ChatHistoryMessage
                    {
                        Id = functionCallContentDeploy.CallId,
                        Role = "assistant",
                        Content = "",
                        Timestamp = msg.CreatedAt,
                        Widget = new ChatHistoryWidget { ToolName = "showDeployment", Data = null }
                    });
                }
            }

            return Results.Ok(new ChatHistoryResponse { Messages = historyMessages });
        })
        .Produces<ChatHistoryResponse>(StatusCodes.Status200OK);
    }
}
