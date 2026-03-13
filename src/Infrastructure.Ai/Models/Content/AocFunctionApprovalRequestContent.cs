namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocFunctionApprovalRequestContent : AocAiContent
{
    public AocFunctionCallContent? FunctionCall { get; set; }
    public string Id { get; set; } = string.Empty;
    public string? ServerName { get; set; }
}
