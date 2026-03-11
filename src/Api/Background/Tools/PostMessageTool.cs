using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Domain.Utils;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

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
                                              "text": { "type": "string", "description": "The text content of the message to post" },
                                              "prevMsgId": { "type": "string", "description": "The unique identifier of the previous message in the conversation" }
                                            },
                                            "required": ["text", "prevMsgId"],
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

        if (arguments == null || !arguments.ContainsKey("prevMsgId") || string.IsNullOrEmpty(arguments["prevMsgId"]?.ToString()))
        {
            return new ToolCallResult(callId, new { ErrorMessage = "prevMsgId param is missing or empty" }, true);
        }

        var text = arguments["text"]!.ToString()!;

        Guid? prevMsgId = null;
        if (!Guid.TryParse(arguments["prevMsgId"]?.ToString(), out var parsedPrevMsgId))
        {
            return new ToolCallResult(callId, new { ErrorMessage = "prevMsgId must be a valid GUID" }, true);
        }
        prevMsgId = parsedPrevMsgId;

        try
        {
            var dbContext = serviceProvider.GetRequiredService<AzureOpsCrewContext>();

            // Validate prevMsgId matches the actual previous message
            var actualPrevMessage = await dbContext.Messages
                .Where(m => (data.Channel != null && m.ChannelId == data.Channel.Id) ||
                            (data.DmChannel != null && m.DmId == data.DmChannel.Id))
                .OrderByDescending(m => m.PostedAt)
                .FirstOrDefaultAsync();

            if (actualPrevMessage != null && actualPrevMessage.Id != prevMsgId)
            {
                return new ToolCallResult(callId,
                    new { ErrorMessage = "prevMsgId does not match the actual previous message id, please use the tool getMessages to fetch missing messages" },
                    true);
            }

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

            dbContext.Messages.Add(message);
            await dbContext.SaveChangesAsync();

            var broadcaster = serviceProvider.GetRequiredService<IChannelEventBroadcaster>();
            if (message.ChannelId.HasValue)
            {
                await broadcaster.BroadcastMessageAddedAsync(message.ChannelId.Value, message);
                var channel = await dbContext.Channels.FindAsync(message.ChannelId.Value);
                if (channel != null)
                {
                    var otherAgentIds = channel.AgentIds.Where(id => id != data.Agent.Id).ToArray();
                    var agentScheduler = serviceProvider.GetRequiredService<AgentScheduler>();
                    foreach (var agentId in otherAgentIds)
                    {
                        var trigger = new MessageTrigger
                        {
                            Id = Guid.NewGuid(),
                            AgentId = agentId,
                            ChatId = channel.Id,
                            CreatedAt = DateTime.UtcNow,
                            MessageId = message.Id,
                            AuthorId = data.Agent.Id,
                            AuthorName = data.Agent.Info.Username,
                            MessageContent = text,
                        };
                        await agentScheduler.QueueTrigger(trigger);
                    }
                }
            }
            else if (message.DmId.HasValue)
            {
                await broadcaster.BroadcastDmMessageAddedAsync(message.DmId.Value, message);
                var dm = await dbContext.Dms.FindAsync(message.DmId.Value);
                if (dm != null)
                {
                    // DirectMessageChannel has Agent1Id and Agent2Id properties
                    // Find the other agent (not the current one) to trigger
                    var otherAgentId = dm.Agent1Id == data.Agent.Id ? dm.Agent2Id : dm.Agent1Id;
                    if (otherAgentId.HasValue)
                    {
                        var agentScheduler = serviceProvider.GetRequiredService<AgentScheduler>();
                        var trigger = new MessageTrigger
                        {
                            Id = Guid.NewGuid(),
                            AgentId = otherAgentId.Value,
                            ChatId = dm.Id,
                            CreatedAt = DateTime.UtcNow,
                            MessageId = message.Id,
                            AuthorId = data.Agent.Id,
                            AuthorName = data.Agent.Info.Username,
                            MessageContent = text,
                        };
                        await agentScheduler.QueueTrigger(trigger);
                    }
                }
            }

            return new ToolCallResult(callId, new { messageId = message.Id }, false);
        }
        catch (Exception e)
        {
            return new ToolCallResult(callId, new { ErrorMessage = e.Message }, true);
        }
    }
}
