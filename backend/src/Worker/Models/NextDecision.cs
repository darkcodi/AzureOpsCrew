namespace Worker.Models;

public record NextDecision(string? FinalAnswer, IReadOnlyList<ToolCall> ToolCalls);
