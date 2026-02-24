namespace Worker.Models;

public record NextStepDecision(
    string? FinalAnswer,
    string? NeedUserQuestion,
    List<McpCall> ToolCalls);
