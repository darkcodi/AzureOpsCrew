namespace Worker.Models.Content;

public class AocFunctionApprovalRequestContent : AocAiContent
{
    public AocFunctionCallContent? FunctionCall { get; set; }
    public string? Id { get; set; }
}
