using System.Text.Json;

namespace Worker.Models.Content;

public sealed class AocAiContentDto
{
    public AocAiContentType ContentType { get; set; }
    public string Content { get; set; } = string.Empty;

    public static AocAiContentDto FromAocAiContent(AocAiContent content)
    {
        var contentType = content switch
        {
            AocCodeInterpreterToolCallContent => AocAiContentType.CodeInterpreterToolCallContent,
            AocCodeInterpreterToolResultContent => AocAiContentType.CodeInterpreterToolResultContent,
            AocDataContent => AocAiContentType.DataContent,
            AocErrorContent => AocAiContentType.ErrorContent,
            AocFunctionApprovalRequestContent => AocAiContentType.FunctionApprovalRequestContent,
            AocFunctionApprovalResponseContent => AocAiContentType.FunctionApprovalResponseContent,
            AocFunctionCallContent => AocAiContentType.FunctionCallContent,
            AocFunctionResultContent => AocAiContentType.FunctionResultContent,
            AocHostedFileContent => AocAiContentType.HostedFileContent,
            AocHostedVectorStoreContent => AocAiContentType.HostedVectorStoreContent,
            AocImageGenerationToolCallContent => AocAiContentType.ImageGenerationToolCallContent,
            AocImageGenerationToolResultContent => AocAiContentType.ImageGenerationToolResultContent,
            AocMcpServerToolApprovalRequestContent => AocAiContentType.McpServerToolApprovalRequestContent,
            AocMcpServerToolApprovalResponseContent => AocAiContentType.McpServerToolApprovalResponseContent,
            AocMcpServerToolCallContent => AocAiContentType.McpServerToolCallContent,
            AocMcpServerToolResultContent => AocAiContentType.McpServerToolResultContent,
            AocTextContent => AocAiContentType.TextContent,
            AocTextReasoningContent => AocAiContentType.TextReasoningContent,
            AocUriContent => AocAiContentType.UriContent,
            AocUsageContent => AocAiContentType.UsageContent,
            AocRunStart => AocAiContentType.RunStart,
            AocRunFinished => AocAiContentType.RunFinished,
            AocRunError => AocAiContentType.RunError,
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
            AocAiContentType.CodeInterpreterToolCallContent => JsonSerializer.Deserialize<AocCodeInterpreterToolCallContent>(Content) ?? new AocCodeInterpreterToolCallContent(),
            AocAiContentType.CodeInterpreterToolResultContent => JsonSerializer.Deserialize<AocCodeInterpreterToolResultContent>(Content) ?? new AocCodeInterpreterToolResultContent(),
            AocAiContentType.DataContent => JsonSerializer.Deserialize<AocDataContent>(Content) ?? new AocDataContent(),
            AocAiContentType.ErrorContent => JsonSerializer.Deserialize<AocErrorContent>(Content) ?? new AocErrorContent(),
            AocAiContentType.FunctionApprovalRequestContent => JsonSerializer.Deserialize<AocFunctionApprovalRequestContent>(Content) ?? new AocFunctionApprovalRequestContent(),
            AocAiContentType.FunctionApprovalResponseContent => JsonSerializer.Deserialize<AocFunctionApprovalResponseContent>(Content) ?? new AocFunctionApprovalResponseContent(),
            AocAiContentType.FunctionCallContent => JsonSerializer.Deserialize<AocFunctionCallContent>(Content) ?? new AocFunctionCallContent(),
            AocAiContentType.FunctionResultContent => JsonSerializer.Deserialize<AocFunctionResultContent>(Content) ?? new AocFunctionResultContent(),
            AocAiContentType.HostedFileContent => JsonSerializer.Deserialize<AocHostedFileContent>(Content) ?? new AocHostedFileContent(),
            AocAiContentType.HostedVectorStoreContent => JsonSerializer.Deserialize<AocHostedVectorStoreContent>(Content) ?? new AocHostedVectorStoreContent(),
            AocAiContentType.ImageGenerationToolCallContent => JsonSerializer.Deserialize<AocImageGenerationToolCallContent>(Content) ?? new AocImageGenerationToolCallContent(),
            AocAiContentType.ImageGenerationToolResultContent => JsonSerializer.Deserialize<AocImageGenerationToolResultContent>(Content) ?? new AocImageGenerationToolResultContent(),
            AocAiContentType.McpServerToolApprovalRequestContent => JsonSerializer.Deserialize<AocMcpServerToolApprovalRequestContent>(Content) ?? new AocMcpServerToolApprovalRequestContent(),
            AocAiContentType.McpServerToolApprovalResponseContent => JsonSerializer.Deserialize<AocMcpServerToolApprovalResponseContent>(Content) ?? new AocMcpServerToolApprovalResponseContent(),
            AocAiContentType.McpServerToolCallContent => JsonSerializer.Deserialize<AocMcpServerToolCallContent>(Content) ?? new AocMcpServerToolCallContent(),
            AocAiContentType.McpServerToolResultContent => JsonSerializer.Deserialize<AocMcpServerToolResultContent>(Content) ?? new AocMcpServerToolResultContent(),
            AocAiContentType.TextContent => JsonSerializer.Deserialize<AocTextContent>(Content) ?? new AocTextContent(),
            AocAiContentType.TextReasoningContent => JsonSerializer.Deserialize<AocTextReasoningContent>(Content) ?? new AocTextReasoningContent(),
            AocAiContentType.UriContent => JsonSerializer.Deserialize<AocUriContent>(Content) ?? new AocUriContent(),
            AocAiContentType.UsageContent => JsonSerializer.Deserialize<AocUsageContent>(Content) ?? new AocUsageContent(),
            AocAiContentType.RunStart => JsonSerializer.Deserialize<AocRunStart>(Content) ?? new AocRunStart(),
            AocAiContentType.RunFinished => JsonSerializer.Deserialize<AocRunFinished>(Content) ?? new AocRunFinished(),
            AocAiContentType.RunError => JsonSerializer.Deserialize<AocRunError>(Content) ?? new AocRunError(),
            _ => throw new ArgumentException($"Unknown content type: {ContentType}"),
        };
    }
}
