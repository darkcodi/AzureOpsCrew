using System.Text.Json.Serialization;

namespace Front.Models;

public class ApprovalRequestDto
{
    private string _status = "pending";

    public string ApprovalId { get; set; } = string.Empty;
    public string ToolName { get; set; } = string.Empty;
    public string CallId { get; set; } = string.Empty;
    public object? Args { get; set; }
    public Guid AgentId { get; set; }
    public string? AgentName { get; set; }
    public string? ServerName { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Reason { get; set; }

    [JsonPropertyName("status")]
    public string Status
    {
        get => _status;
        set
        {
            _status = string.IsNullOrWhiteSpace(value) ? "pending" : value;
            UserResponse = _status.ToLowerInvariant() switch
            {
                "approved" => true,
                "rejected" => false,
                _ => null,
            };
        }
    }

    public bool? UserResponse { get; set; }
}
