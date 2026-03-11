using System.Text.Json;

namespace Front.Models;

public class ToolCallDto
{
    public string ToolName { get; set; } = string.Empty;
    public string CallId { get; set; } = string.Empty;
    public JsonElement? Args { get; set; }
    public JsonElement? Result { get; set; }
    public bool IsError { get; set; }
    public bool IsFinished { get; set; }
    public Guid AgentId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
