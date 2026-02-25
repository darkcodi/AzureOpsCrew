namespace Worker.Models.Content;

public sealed class AocFunctionApprovalResponseContent : AocAiContent
{
    public bool Approved { get; set; }
    public AocFunctionCallContent? FunctionCall { get; set; }
    public string? Reason { get; set; }
    public string Id { get; set; } = string.Empty;
}
