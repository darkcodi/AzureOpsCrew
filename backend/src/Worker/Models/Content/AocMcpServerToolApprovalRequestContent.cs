namespace Worker.Models.Content;

public class AocMcpServerToolApprovalRequestContent : AocAiContent
{
    public AocMcpServerToolCallContent? ToolCall { get; set; }
    public string? Id { get; set; }
}
