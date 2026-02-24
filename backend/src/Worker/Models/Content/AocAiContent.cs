using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.AI;
using Serilog;

namespace Worker.Models.Content;

#pragma warning disable MEAI001

public abstract class AocAiContent
{
    public static AocAiContent? Parse(AIContent content)
    {
        switch (content)
        {
            case CodeInterpreterToolCallContent c:
                return new AocCodeInterpreterToolCallContent
                {
                    CallId = c.CallId,
                    Inputs = (c.Inputs ?? new List<AIContent>()).Select(Parse).Where(x => x != null).Select(x => x!).ToList(),
                };
            case CodeInterpreterToolResultContent c:
                return new AocCodeInterpreterToolResultContent
                {
                    CallId = c.CallId,
                    Outputs = (c.Outputs ?? new List<AIContent>()).Select(Parse).Where(x => x != null).Select(x => x!).ToList(),
                };
            case DataContent:
            case ErrorContent:
            case FunctionApprovalRequestContent:
            case FunctionApprovalResponseContent:
            case FunctionCallContent:
            case FunctionResultContent:
            case HostedFileContent:
            case HostedVectorStoreContent:
            case ImageGenerationToolCallContent:
            case ImageGenerationToolResultContent:
            case McpServerToolApprovalRequestContent:
            case McpServerToolApprovalResponseContent:
            case McpServerToolCallContent:
            case McpServerToolResultContent:
            case TextContent:
            case TextReasoningContent:
            case UriContent:
            case UsageContent:
            case UserInputRequestContent:
            case UserInputResponseContent:
            {
                Log.Information("Not implemented parsing for content type {ContentType}", content.GetType().Name);
                return null;
            }
            default:
            {
                Log.Warning("Unknown content type {ContentType}", content.GetType().Name);
                return null;
            }
        }
    }
}
