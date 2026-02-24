namespace Worker.Models;

public record AskResult(
    string TurnId,
    string Answer,
    IReadOnlyList<ToolTrace> ToolsUsed);
