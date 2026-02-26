using AzureOpsCrew.Api.Endpoints.Dtos.ChatHistory;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
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

                // Only include TextContent for user-visible messages
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
            }

            return Results.Ok(new ChatHistoryResponse { Messages = historyMessages });
        })
        .Produces<ChatHistoryResponse>(StatusCodes.Status200OK);
    }
}
