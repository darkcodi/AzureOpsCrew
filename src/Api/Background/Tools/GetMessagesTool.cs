using System.Globalization;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Api.Background.Tools;

public class GetMessagesTool : IBackendTool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "getMessages",
            Description = "Gets messages from the current channel or direct message conversation. Optionally filters to only messages posted after a given timestamp.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "after": { "type": "string", "description": "Optional ISO 8601 date-time. When provided, only messages posted after this timestamp are returned." }
                                            },
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                  "type": "object",
                                                  "properties": {
                                                    "messages": {
                                                      "type": "array",
                                                      "items": {
                                                        "type": "object",
                                                        "properties": {
                                                          "id": { "type": "string", "description": "The unique identifier of the message" },
                                                          "text": { "type": "string", "description": "The message text content" },
                                                          "postedAt": { "type": "string", "description": "The date and time the message was posted (ISO 8601)" },
                                                          "authorName": { "type": ["string", "null"], "description": "The name of the message author" },
                                                          "isAgentMessage": { "type": "boolean", "description": "Whether the message was posted by an agent" }
                                                        },
                                                        "required": ["id", "text", "postedAt", "isAgentMessage"],
                                                        "additionalProperties": false
                                                      },
                                                      "description": "All messages in the current conversation"
                                                    }
                                                  },
                                                  "required": ["messages"],
                                                  "additionalProperties": false
                                                }
                                                """).ToString(),
            ToolType = ToolType.BackEnd,
        };
    }

    public Task<ToolCallResult> ExecuteAsync(AgentRunData data, string callId, IDictionary<string, object?>? arguments, IServiceProvider serviceProvider)
    {
        IEnumerable<Domain.Chats.Message> source = data.ChatMessages;

        if (arguments != null && arguments.TryGetValue("after", out var afterValue))
        {
            if (afterValue is string afterStr &&
                DateTime.TryParse(afterStr, null, DateTimeStyles.RoundtripKind, out var fromDate))
            {
                source = source.Where(m => m.PostedAt >= fromDate.ToUniversalTime());
            }
            else
            {
                return Task.FromResult(new ToolCallResult(callId, new { ErrorMessage = "after param is not a valid ISO 8601 date-time string" }, true));
            }
        }

        var messages = source.Select(m => new
        {
            id = m.Id,
            text = m.Text,
            postedAt = m.PostedAt.ToString("o"),
            authorName = m.AuthorName,
            isAgentMessage = m.AgentId.HasValue,
        }).ToList();

        return Task.FromResult(new ToolCallResult(callId, new { messages }, false));
    }
}
