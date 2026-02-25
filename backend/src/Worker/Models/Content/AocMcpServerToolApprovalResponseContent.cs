namespace Worker.Models.Content;

public class AocMcpServerToolApprovalResponseContent : AocAiContent
{
    public bool Approved { get; set; }
    public string? Id { get; set; }
}
