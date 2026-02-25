using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;
using Serilog;

namespace Worker.Models.Content;

#pragma warning disable MEAI001

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(AocCodeInterpreterToolCallContent), nameof(AocCodeInterpreterToolCallContent))]
[JsonDerivedType(typeof(AocCodeInterpreterToolResultContent), nameof(AocCodeInterpreterToolResultContent))]
[JsonDerivedType(typeof(AocDataContent), nameof(AocDataContent))]
[JsonDerivedType(typeof(AocErrorContent), nameof(AocErrorContent))]
[JsonDerivedType(typeof(AocFunctionApprovalRequestContent), nameof(AocFunctionApprovalRequestContent))]
[JsonDerivedType(typeof(AocFunctionApprovalResponseContent), nameof(AocFunctionApprovalResponseContent))]
[JsonDerivedType(typeof(AocFunctionCallContent), nameof(AocFunctionCallContent))]
[JsonDerivedType(typeof(AocFunctionResultContent), nameof(AocFunctionResultContent))]
[JsonDerivedType(typeof(AocHostedFileContent), nameof(AocHostedFileContent))]
[JsonDerivedType(typeof(AocHostedVectorStoreContent), nameof(AocHostedVectorStoreContent))]
[JsonDerivedType(typeof(AocImageGenerationToolCallContent), nameof(AocImageGenerationToolCallContent))]
[JsonDerivedType(typeof(AocImageGenerationToolResultContent), nameof(AocImageGenerationToolResultContent))]
[JsonDerivedType(typeof(AocMcpServerToolApprovalRequestContent), nameof(AocMcpServerToolApprovalRequestContent))]
[JsonDerivedType(typeof(AocMcpServerToolApprovalResponseContent), nameof(AocMcpServerToolApprovalResponseContent))]
[JsonDerivedType(typeof(AocMcpServerToolCallContent), nameof(AocMcpServerToolCallContent))]
[JsonDerivedType(typeof(AocMcpServerToolResultContent), nameof(AocMcpServerToolResultContent))]
[JsonDerivedType(typeof(AocTextContent), nameof(AocTextContent))]
[JsonDerivedType(typeof(AocTextReasoningContent), nameof(AocTextReasoningContent))]
[JsonDerivedType(typeof(AocUriContent), nameof(AocUriContent))]
[JsonDerivedType(typeof(AocUsageContent), nameof(AocUsageContent))]
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
                    InputTokenCount = c.Details.InputTokenCount,
                    OutputTokenCount = c.Details.OutputTokenCount,
                    TotalTokenCount = c.Details.TotalTokenCount,
                    CachedInputTokenCount = c.Details.CachedInputTokenCount,
                    ReasoningTokenCount = c.Details.ReasoningTokenCount,
                    AdditionalCounts = c.Details.AdditionalCounts?.ToDictionary(x => x.Key, x => x.Value)
                };
            default:
            {
                Log.Warning("Unknown content type {ContentType}", content.GetType().Name);
                return null;
            }
        }
    }

    public AIContent? ToAiContent()
    {
        switch (this)
        {
            case AocCodeInterpreterToolCallContent c:
                return new CodeInterpreterToolCallContent
                {
                    CallId = c.CallId,
                    Inputs = c.Inputs.Select(x => x.ToAiContent()).Where(x => x != null).Select(x => x!).ToList(),
                };
            case AocCodeInterpreterToolResultContent c:
                return new CodeInterpreterToolResultContent
                {
                    CallId = c.CallId,
                    Outputs = c.Outputs.Select(x => x.ToAiContent()).Where(x => x != null).Select(x => x!).ToList(),
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
            {
                var functionCallContent = c.FunctionCall?.ToAiContent() as FunctionCallContent;
                if (functionCallContent == null)
                {
                    Log.Warning("FunctionApprovalRequestContent with id {Id} has invalid FunctionCallContent", c.Id);
                    return null;
                }
                return new FunctionApprovalRequestContent(c.Id, functionCallContent);
            }
            case AocFunctionApprovalResponseContent c:
            {
                var functionCallContent = c.FunctionCall?.ToAiContent() as FunctionCallContent;
                if (functionCallContent == null)
                {
                    Log.Warning("FunctionApprovalRequestContent with id {Id} has invalid FunctionCallContent", c.Id);
                    return null;
                }
                return new FunctionApprovalResponseContent(c.Id, c.Approved, functionCallContent)
                {
                    Reason = c.Reason,
                };
            }
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
                    Outputs = c.Outputs?.Select(x => x.ToAiContent()).Where(x => x != null).Select(x => x!).ToList(),
                };
            case AocMcpServerToolApprovalRequestContent c:
            {
                var toolCallContent = c.ToolCall?.ToAiContent() as McpServerToolCallContent;
                if (toolCallContent == null)
                {
                    Log.Warning("McpServerToolApprovalRequestContent with id {Id} has invalid McpServerToolCallContent", c.Id);
                    return null;
                }
                return new McpServerToolApprovalRequestContent(c.Id, toolCallContent);
            }
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
                    Output = c.Output?.Select(x => x.ToAiContent()).Where(x => x != null).Select(x => x!).ToList(),
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
            {
                var additionalCounts = c.AdditionalCounts != null
                    ? new AdditionalPropertiesDictionary<long>(c.AdditionalCounts)
                    : null;
                return new UsageContent
                {
                    Details = new UsageDetails
                    {
                        InputTokenCount = c.InputTokenCount,
                        OutputTokenCount = c.OutputTokenCount,
                        TotalTokenCount = c.TotalTokenCount,
                        CachedInputTokenCount = c.CachedInputTokenCount,
                        ReasoningTokenCount = c.ReasoningTokenCount,
                        AdditionalCounts = additionalCounts,
                    }
                };
            }
            default:
                throw new InvalidOperationException($"Unknown AocAiContent type {GetType().Name}");
        }
    }
}
