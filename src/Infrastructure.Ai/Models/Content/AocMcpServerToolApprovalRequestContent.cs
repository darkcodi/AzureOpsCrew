namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocMcpServerToolApprovalRequestContent : AocAiContent
{
    public AocMcpServerToolCallContent? ToolCall { get; set; }
    public string Id { get; set; } = string.Empty;
}
