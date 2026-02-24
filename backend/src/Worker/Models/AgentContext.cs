namespace Worker.Models;

public record AgentContext(
    IReadOnlyList<(string Role, string Content)> TranscriptTail,
    string UserText,
    IReadOnlyList<ToolResult> ToolResults = null!);
