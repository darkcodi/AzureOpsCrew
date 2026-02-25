using System.Text.Json;
using AzureOpsCrew.Domain.Chats;

namespace Worker.Models.Content;

public sealed class AocAiContentDto
{
    public LlmMessageContentType ContentType { get; set; }
    public string Content { get; set; } = string.Empty;

    public static AocAiContentDto FromAocAiContent(AocAiContent content)
    {
        var contentType = content switch
        {
            AocCodeInterpreterToolCallContent => LlmMessageContentType.CodeInterpreterToolCallContent,
            AocCodeInterpreterToolResultContent => LlmMessageContentType.CodeInterpreterToolResultContent,
            AocDataContent => LlmMessageContentType.DataContent,
            AocErrorContent => LlmMessageContentType.ErrorContent,
            AocFunctionApprovalRequestContent => LlmMessageContentType.FunctionApprovalRequestContent,
            AocFunctionApprovalResponseContent => LlmMessageContentType.FunctionApprovalResponseContent,
            AocFunctionCallContent => LlmMessageContentType.FunctionCallContent,
            AocFunctionResultContent => LlmMessageContentType.FunctionResultContent,
            AocHostedFileContent => LlmMessageContentType.HostedFileContent,
            AocHostedVectorStoreContent => LlmMessageContentType.HostedVectorStoreContent,
            AocImageGenerationToolCallContent => LlmMessageContentType.ImageGenerationToolCallContent,
            AocImageGenerationToolResultContent => LlmMessageContentType.ImageGenerationToolResultContent,
            AocMcpServerToolApprovalRequestContent => LlmMessageContentType.McpServerToolApprovalRequestContent,
            AocMcpServerToolApprovalResponseContent => LlmMessageContentType.McpServerToolApprovalResponseContent,
            AocMcpServerToolCallContent => LlmMessageContentType.McpServerToolCallContent,
            AocMcpServerToolResultContent => LlmMessageContentType.McpServerToolResultContent,
            AocTextContent => LlmMessageContentType.TextContent,
            AocTextReasoningContent => LlmMessageContentType.TextReasoningContent,
            AocUriContent => LlmMessageContentType.UriContent,
            AocUsageContent => LlmMessageContentType.UsageContent,
            AocRunStart => LlmMessageContentType.RunStart,
            AocRunFinished => LlmMessageContentType.RunFinished,
            AocRunError => LlmMessageContentType.RunError,
            _ => throw new ArgumentException($"Unknown content type: {content.GetType().Name}"),
        };
        var contentJson = content switch
        {
            AocCodeInterpreterToolCallContent c => JsonSerializer.Serialize(c),
            AocCodeInterpreterToolResultContent c => JsonSerializer.Serialize(c),
            AocDataContent c => JsonSerializer.Serialize(c),
            AocErrorContent c => JsonSerializer.Serialize(c),
            AocFunctionApprovalRequestContent c => JsonSerializer.Serialize(c),
            AocFunctionApprovalResponseContent c => JsonSerializer.Serialize(c),
            AocFunctionCallContent c => JsonSerializer.Serialize(c),
            AocFunctionResultContent c => JsonSerializer.Serialize(c),
            AocHostedFileContent c => JsonSerializer.Serialize(c),
            AocHostedVectorStoreContent c => JsonSerializer.Serialize(c),
            AocImageGenerationToolCallContent c => JsonSerializer.Serialize(c),
            AocImageGenerationToolResultContent c => JsonSerializer.Serialize(c),
            AocMcpServerToolApprovalRequestContent c => JsonSerializer.Serialize(c),
            AocMcpServerToolApprovalResponseContent c => JsonSerializer.Serialize(c),
            AocMcpServerToolCallContent c => JsonSerializer.Serialize(c),
            AocMcpServerToolResultContent c => JsonSerializer.Serialize(c),
            AocTextContent c => JsonSerializer.Serialize(c),
            AocTextReasoningContent c => JsonSerializer.Serialize(c),
            AocUriContent c => JsonSerializer.Serialize(c),
            AocUsageContent c => JsonSerializer.Serialize(c),
            AocRunStart c => JsonSerializer.Serialize(c),
            AocRunFinished c => JsonSerializer.Serialize(c),
            AocRunError c => JsonSerializer.Serialize(c),
            _ => throw new ArgumentException($"Unknown content type: {content.GetType().Name}"),
        };
        return new AocAiContentDto
        {
            ContentType = contentType,
            Content = contentJson,
        };
    }

    public AocAiContent ToAocAiContent()
    {
        return ContentType switch
        {
            LlmMessageContentType.CodeInterpreterToolCallContent => JsonSerializer.Deserialize<AocCodeInterpreterToolCallContent>(Content) ?? new AocCodeInterpreterToolCallContent(),
            LlmMessageContentType.CodeInterpreterToolResultContent => JsonSerializer.Deserialize<AocCodeInterpreterToolResultContent>(Content) ?? new AocCodeInterpreterToolResultContent(),
            LlmMessageContentType.DataContent => JsonSerializer.Deserialize<AocDataContent>(Content) ?? new AocDataContent(),
            LlmMessageContentType.ErrorContent => JsonSerializer.Deserialize<AocErrorContent>(Content) ?? new AocErrorContent(),
            LlmMessageContentType.FunctionApprovalRequestContent => JsonSerializer.Deserialize<AocFunctionApprovalRequestContent>(Content) ?? new AocFunctionApprovalRequestContent(),
            LlmMessageContentType.FunctionApprovalResponseContent => JsonSerializer.Deserialize<AocFunctionApprovalResponseContent>(Content) ?? new AocFunctionApprovalResponseContent(),
            LlmMessageContentType.FunctionCallContent => JsonSerializer.Deserialize<AocFunctionCallContent>(Content) ?? new AocFunctionCallContent(),
            LlmMessageContentType.FunctionResultContent => JsonSerializer.Deserialize<AocFunctionResultContent>(Content) ?? new AocFunctionResultContent(),
            LlmMessageContentType.HostedFileContent => JsonSerializer.Deserialize<AocHostedFileContent>(Content) ?? new AocHostedFileContent(),
            LlmMessageContentType.HostedVectorStoreContent => JsonSerializer.Deserialize<AocHostedVectorStoreContent>(Content) ?? new AocHostedVectorStoreContent(),
            LlmMessageContentType.ImageGenerationToolCallContent => JsonSerializer.Deserialize<AocImageGenerationToolCallContent>(Content) ?? new AocImageGenerationToolCallContent(),
            LlmMessageContentType.ImageGenerationToolResultContent => JsonSerializer.Deserialize<AocImageGenerationToolResultContent>(Content) ?? new AocImageGenerationToolResultContent(),
            LlmMessageContentType.McpServerToolApprovalRequestContent => JsonSerializer.Deserialize<AocMcpServerToolApprovalRequestContent>(Content) ?? new AocMcpServerToolApprovalRequestContent(),
            LlmMessageContentType.McpServerToolApprovalResponseContent => JsonSerializer.Deserialize<AocMcpServerToolApprovalResponseContent>(Content) ?? new AocMcpServerToolApprovalResponseContent(),
            LlmMessageContentType.McpServerToolCallContent => JsonSerializer.Deserialize<AocMcpServerToolCallContent>(Content) ?? new AocMcpServerToolCallContent(),
            LlmMessageContentType.McpServerToolResultContent => JsonSerializer.Deserialize<AocMcpServerToolResultContent>(Content) ?? new AocMcpServerToolResultContent(),
            LlmMessageContentType.TextContent => JsonSerializer.Deserialize<AocTextContent>(Content) ?? new AocTextContent(),
            LlmMessageContentType.TextReasoningContent => JsonSerializer.Deserialize<AocTextReasoningContent>(Content) ?? new AocTextReasoningContent(),
            LlmMessageContentType.UriContent => JsonSerializer.Deserialize<AocUriContent>(Content) ?? new AocUriContent(),
            LlmMessageContentType.UsageContent => JsonSerializer.Deserialize<AocUsageContent>(Content) ?? new AocUsageContent(),
            LlmMessageContentType.RunStart => JsonSerializer.Deserialize<AocRunStart>(Content) ?? new AocRunStart(),
            LlmMessageContentType.RunFinished => JsonSerializer.Deserialize<AocRunFinished>(Content) ?? new AocRunFinished(),
            LlmMessageContentType.RunError => JsonSerializer.Deserialize<AocRunError>(Content) ?? new AocRunError(),
            _ => throw new ArgumentException($"Unknown content type: {ContentType}"),
        };
    }
}
