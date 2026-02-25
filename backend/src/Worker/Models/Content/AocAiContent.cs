using Microsoft.Extensions.AI;
using Serilog;

namespace Worker.Models.Content;

#pragma warning disable MEAI001

public abstract class AocAiContent
{
    public static AocAiContent? FromAiContent(AIContent content)
    {
        switch (content)
        {
            case CodeInterpreterToolCallContent c:
                return new AocCodeInterpreterToolCallContent
                {
                    CallId = c.CallId,
                    Inputs = (c.Inputs ?? new List<AIContent>()).Select(FromAiContent).Where(x => x != null)
                        .Select(x => x!).ToList(),
                };
            case CodeInterpreterToolResultContent c:
                return new AocCodeInterpreterToolResultContent
                {
                    CallId = c.CallId,
                    Outputs = (c.Outputs ?? new List<AIContent>()).Select(FromAiContent).Where(x => x != null)
                        .Select(x => x!).ToList(),
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
                    FunctionCall = FromAiContent(c.FunctionCall) as AocFunctionCallContent,
                };
            case FunctionApprovalResponseContent c:
                return new AocFunctionApprovalResponseContent
                {
                    Id = c.Id,
                    Approved = c.Approved,
                    Reason = c.Reason,
                    FunctionCall = FromAiContent(c.FunctionCall) as AocFunctionCallContent,
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
                    Outputs = (c.Outputs ?? new List<AIContent>()).Select(FromAiContent).Where(x => x != null)
                        .Select(x => x!).ToList(),
                };
            case McpServerToolApprovalRequestContent c:
                return new AocMcpServerToolApprovalRequestContent
                {
                    Id = c.Id,
                    ToolCall = FromAiContent(c.ToolCall) as AocMcpServerToolCallContent,
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
                    Output = (c.Output ?? new List<AIContent>()).Select(FromAiContent).Where(x => x != null)
                        .Select(x => x!).ToList(),
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

    public static AIContent ToAiContent(AocAiContent content)
    {
        switch (content)
        {
            case AocCodeInterpreterToolCallContent c:
                return new CodeInterpreterToolCallContent
                {
                    CallId = c.CallId,
                    Inputs = c.Inputs?.Select(ToAiContent).ToList() ?? new List<AIContent>(),
                };
            case AocCodeInterpreterToolResultContent c:
                return new CodeInterpreterToolResultContent
                {
                    CallId = c.CallId,
                    Outputs = c.Outputs?.Select(ToAiContent).ToList() ?? new List<AIContent>(),
                };
            case AocDataContent c:
                return new DataContent(c.Uri, c.MediaType);
            case AocErrorContent c:
                return new ErrorContent(c.Message)
                {
                    ErrorCode = c.ErrorCode,
                    Details = c.Details,
                };
            case AocFunctionApprovalRequestContent c:
                return new FunctionApprovalRequestContent(c.Id, ToAiContent(c.FunctionCall) as FunctionCallContent);
            case AocFunctionApprovalResponseContent c:
                return new FunctionApprovalResponseContent(c.Id, c.Approved, ToAiContent(c.FunctionCall) as FunctionCallContent)
                {
                    Reason = c.Reason,
                };
            case AocFunctionCallContent c:
                return new FunctionCallContent(c.CallId, c.Name, c.Arguments)
                {
                    InformationalOnly = c.InformationalOnly,
                };
            case AocFunctionResultContent c:
                return new FunctionResultContent(c.CallId, c.Result);
            case AocHostedFileContent c:
                return new HostedFileContent(c.FileId)
                {
                    MediaType = c.MediaType,
                    Name = c.Name,
                };
            case AocHostedVectorStoreContent c:
                return new HostedVectorStoreContent(c.VectorStoreId);
            case AocImageGenerationToolCallContent c:
                return new ImageGenerationToolCallContent
                {
                    ImageId = c.ImageId,
                };
            case AocImageGenerationToolResultContent c:
                return new ImageGenerationToolResultContent
                {
                    ImageId = c.ImageId,
                    Outputs = c.Outputs?.Select(ToAiContent).ToList() ?? new List<AIContent>(),
                };
            case AocMcpServerToolApprovalRequestContent c:
                return new McpServerToolApprovalRequestContent(c.Id, ToAiContent(c.ToolCall) as McpServerToolCallContent);
            case AocMcpServerToolApprovalResponseContent c:
                return new McpServerToolApprovalResponseContent(c.Id, c.Approved);
            case AocMcpServerToolCallContent c:
                return new McpServerToolCallContent(c.CallId, c.ToolName, c.ServerName)
                {
                    Arguments = c.Arguments
                };
            case AocMcpServerToolResultContent c:
                return new McpServerToolResultContent(c.CallId)
                {
                    Output = c.Output?.Select(ToAiContent).ToList() ?? new List<AIContent>(),
                };
            case AocTextContent c:
                return new TextContent(c.Text);
            case AocTextReasoningContent c:
                return new TextReasoningContent(c.Text)
                {
                    ProtectedData = c.ProtectedData,
                };
            case AocUriContent c:
                return new UriContent(c.Uri, c.MediaType);
            case AocUsageContent c:
                return new UsageContent
                {
                    Details = new UsageDetails
                    {
                        InputTokenCount = c.InputTokenCount,
                        OutputTokenCount = c.OutputTokenCount,
                        TotalTokenCount = c.TotalTokenCount,
                        CachedInputTokenCount = c.CachedInputTokenCount,
                        ReasoningTokenCount = c.ReasoningTokenCount,
                        AdditionalCounts = new AdditionalPropertiesDictionary<long>(c.AdditionalCounts),
                    }
                };
            // case AocUserInputRequestContent c:
            //     return new UserInputRequestContent(c.Id);
            // case AocUserInputResponseContent c:
            //     return new UserInputResponseContent(c.Id);
            default:
                throw new InvalidOperationException($"Unknown AocAiContent type {content.GetType().Name}");
        }
    }
}
