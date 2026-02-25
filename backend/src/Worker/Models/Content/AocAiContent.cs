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
            case DataContent c:
                return new AocDataContent
                {
                    MediaType = c.MediaType,
                    Data = c.Data.IsEmpty ? null : c.Data.ToArray(),
                    Uri = c.Uri,
                };
            case ErrorContent c:
                return new AocErrorContent
                {
                    Message = c.Message,
                    ErrorCode = c.ErrorCode,
                    Details = c.Details,
                };
            case FunctionApprovalRequestContent c:
                return new AocFunctionApprovalRequestContent
                {
                    Id = c.Id,
                    FunctionCall = Parse(c.FunctionCall) as AocFunctionCallContent,
                };
            case FunctionApprovalResponseContent c:
                return new AocFunctionApprovalResponseContent
                {
                    Id = c.Id,
                    Approved = c.Approved,
                    Reason = c.Reason,
                    FunctionCall = Parse(c.FunctionCall) as AocFunctionCallContent,
                };
            case FunctionCallContent c:
                return new AocFunctionCallContent
                {
                    CallId = c.CallId,
                    Name = c.Name,
                    Arguments = c.Arguments,
                    InformationalOnly = c.InformationalOnly,
                };
            case FunctionResultContent c:
                return new AocFunctionResultContent
                {
                    CallId = c.CallId,
                    Result = c.Result,
                };
            case HostedFileContent c:
                return new AocHostedFileContent
                {
                    FileId = c.FileId,
                    MediaType = c.MediaType,
                    Name = c.Name,
                };
            case HostedVectorStoreContent c:
                return new AocHostedVectorStoreContent
                {
                    VectorStoreId = c.VectorStoreId,
                };
            case ImageGenerationToolCallContent c:
                return new AocImageGenerationToolCallContent
                {
                    ImageId = c.ImageId,
                };
            case ImageGenerationToolResultContent c:
                return new AocImageGenerationToolResultContent
                {
                    ImageId = c.ImageId,
                    Outputs = (c.Outputs ?? new List<AIContent>()).Select(Parse).Where(x => x != null).Select(x => x!).ToList(),
                };
            case McpServerToolApprovalRequestContent c:
                return new AocMcpServerToolApprovalRequestContent
                {
                    Id = c.Id,
                    ToolCall = Parse(c.ToolCall) as AocMcpServerToolCallContent,
                };
            case McpServerToolApprovalResponseContent c:
                return new AocMcpServerToolApprovalResponseContent
                {
                    Id = c.Id,
                    Approved = c.Approved,
                };
            case McpServerToolCallContent c:
                return new AocMcpServerToolCallContent
                {
                    CallId = c.CallId,
                    ToolName = c.ToolName,
                    ServerName = c.ServerName,
                    Arguments = c.Arguments
                };
            case McpServerToolResultContent c:
                return new AocMcpServerToolResultContent
                {
                    CallId = c.CallId,
                    Output = (c.Output ?? new List<AIContent>()).Select(Parse).Where(x => x != null).Select(x => x!).ToList(),
                };
            case TextContent c:
                return new AocTextContent
                {
                    Text = c.Text,
                };
            case TextReasoningContent c:
                return new AocTextReasoningContent
                {
                    Text = c.Text,
                    ProtectedData = c.ProtectedData,
                };
            case UriContent c:
                return new AocUriContent
                {
                    Uri = c.Uri,
                    MediaType = c.MediaType
                };
            case UsageContent c:
                return new AocUsageContent
                {
                    InputTokenCount = c.Details?.InputTokenCount,
                    OutputTokenCount = c.Details?.OutputTokenCount,
                    TotalTokenCount = c.Details?.TotalTokenCount,
                    CachedInputTokenCount = c.Details?.CachedInputTokenCount,
                    ReasoningTokenCount = c.Details?.ReasoningTokenCount,
                    AdditionalCounts = c.Details?.AdditionalCounts?.ToDictionary(x => x.Key, x => x.Value)
                };
            case UserInputRequestContent c:
                return new AocUserInputRequestContent
                {
                    Id = c.Id,
                };
            case UserInputResponseContent c:
                return new AocUserInputResponseContent
                {
                    Id = c.Id,
                };
            default:
            {
                Log.Warning("Unknown content type {ContentType}", content.GetType().Name);
                return null;
            }
        }
    }
}
