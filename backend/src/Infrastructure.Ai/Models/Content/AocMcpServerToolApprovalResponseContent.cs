namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocMcpServerToolApprovalResponseContent : AocAiContent
{
    public bool Approved { get; set; }
    public string Id { get; set; } = string.Empty;
}
