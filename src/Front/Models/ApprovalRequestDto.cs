using System.Text.Json;

namespace Front.Models;

public class ApprovalRequestDto
{
    public string ApprovalId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string CallId { get; set; } = string.Empty;
    public object? Args { get; set; }
    public Guid AgentId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    /// <summary>
    /// null = pending, true = approved, false = rejected
    /// </summary>
    public bool? UserResponse { get; set; }
}
