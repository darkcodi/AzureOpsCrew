using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Domain.Utils;
using AzureOpsCrew.Infrastructure.Db;

namespace AzureOpsCrew.Api.Background.Tools;

public class PostMessageTool : IBackendTool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "postMessage",
            Description = "Posts a new message to the current channel or direct message conversation on behalf of the agent.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "text": { "type": "string", "description": "The text content of the message to post" }
                                            },
                                            "required": ["text"],
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                  "type": "object",
                                                  "properties": {
                                                    "messageId": { "type": "string", "description": "The unique identifier of the posted message" }
                                                  },
                                                  "required": ["messageId"],
                                                  "additionalProperties": false
                                                }
                                                """).ToString(),
            ToolType = ToolType.BackEnd,
        };
    }

    public async Task<ToolCallResult> ExecuteAsync(AgentRunData data, string callId, IDictionary<string, object?>? arguments, IServiceProvider serviceProvider)
    {
        if (arguments == null || !arguments.ContainsKey("text") || string.IsNullOrEmpty(arguments["text"]?.ToString()))
        {
            return new ToolCallResult(callId, new { ErrorMessage = "text param is missing or empty" }, true);
        }

        var text = arguments["text"]!.ToString()!;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            Text = text,
            PostedAt = DateTime.UtcNow,
            AuthorName = data.Agent.Info.Username,
            AgentId = data.Agent.Id,
            ChannelId = data.Channel?.Id,
            DmId = data.DmChannel?.Id,
        };

        try
        {
            var dbContext = serviceProvider.GetRequiredService<AzureOpsCrewContext>();
            dbContext.Messages.Add(message);
            await dbContext.SaveChangesAsync();

            var broadcaster = serviceProvider.GetRequiredService<IChannelEventBroadcaster>();
            if (message.ChannelId.HasValue)
            {
                await broadcaster.BroadcastMessageAddedAsync(message.ChannelId.Value, message);
            }
            else if (message.DmId.HasValue)
            {
                await broadcaster.BroadcastDmMessageAddedAsync(message.DmId.Value, message);
            }

            return new ToolCallResult(callId, new { messageId = message.Id }, false);
        }
        catch (Exception e)
        {
            return new ToolCallResult(callId, new { ErrorMessage = e.Message }, true);
        }
    }
}
