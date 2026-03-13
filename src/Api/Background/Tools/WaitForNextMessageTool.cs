using AzureOpsCrew.Api.Services;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Chats;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Domain.Utils;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace AzureOpsCrew.Api.Background.Tools;

public class WaitForNextMessageTool : IBackendTool
{
    public static string ToolName => "waitForNextMessage";

    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = ToolName,
            Description = "Waits for the next message in the chat after the specified message ID. Use this when you want to skip your turn and let other agents or the human respond. The system will automatically give you a new turn when there are new messages in the chat.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "afterMsgId": { "type": "string", "description": "The ID of the last message you've seen. You will get a new turn when a message after this one is posted.", "format": "uuid" }
                                            },
                                            "required": ["afterMsgId"],
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                    "type": "object",
                                                    "properties": { },
                                                    "required": [],
                                                    "additionalProperties": false
                                                }
                                                """).ToString(),
            ToolType = ToolType.BackEnd,
        };
    }

    public async Task<ToolCallResult> ExecuteAsync(AgentRunData data, string callId, IDictionary<string, object?>? arguments, IServiceProvider serviceProvider)
    {
        // validate arguments
        if (arguments == null || !arguments.TryGetValue("afterMsgId", out var afterMsgIdObj) || afterMsgIdObj == null || !Guid.TryParse(afterMsgIdObj.ToString(), out var afterMsgId))
        {
            return new ToolCallResult(callId, new { ErrorMessage = "Invalid or missing 'afterMsgId' argument. It should be a valid UUID string." }, false);
        }

        try
        {
            var dbContext = serviceProvider.GetRequiredService<AzureOpsCrewContext>();

            // Validate afterMsgId exists
            var referencedMessage = await dbContext.Messages.FirstOrDefaultAsync(m => m.Id == afterMsgId);
            if (referencedMessage == null)
            {
                return new ToolCallResult(callId,
                    new { ErrorMessage = "afterMsgId is invalid: no message with such id exists, please use the tool getMessages to fetch the last message" },
                    true);
            }

            // Validate that afterMsgId is in the last message in the chat
            var lastMessageInChat = await dbContext.Messages.Where(m => m.ChannelId == referencedMessage.ChannelId && m.DmId == referencedMessage.DmId).OrderByDescending(m => m.PostedAt).FirstOrDefaultAsync();
            if (lastMessageInChat == null || lastMessageInChat.Id != afterMsgId)
            {
                return new ToolCallResult(callId,
                    new { ErrorMessage = "afterMsgId is not the last message in the chat, please use the tool getMessages to fetch the last message and use its id" },
                    true);
            }

            return new ToolCallResult(callId, new { FinishedWaiting = true }, false);
        }
        catch (Exception e)
        {
            return new ToolCallResult(callId, new { ErrorMessage = e.Message }, true);
        }
    }
}
