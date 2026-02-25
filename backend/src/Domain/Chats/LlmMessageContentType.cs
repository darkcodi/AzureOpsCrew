namespace AzureOpsCrew.Domain.Chats;

public enum LlmMessageContentType
{
    None = 0,

    // mapped from MAF content types
    CodeInterpreterToolCallContent,
    CodeInterpreterToolResultContent,
    DataContent,
    ErrorContent,
    FunctionApprovalRequestContent,
    FunctionApprovalResponseContent,
    FunctionCallContent,
    FunctionResultContent,
    HostedFileContent,
    HostedVectorStoreContent,
    ImageGenerationToolCallContent,
    ImageGenerationToolResultContent,
    McpServerToolApprovalRequestContent,
    McpServerToolApprovalResponseContent,
    McpServerToolCallContent,
    McpServerToolResultContent,
    TextContent,
    TextReasoningContent,
    UriContent,
    UsageContent,

    // our custom system content types
    RunStart,
    RunFinished,
    RunError,
}
