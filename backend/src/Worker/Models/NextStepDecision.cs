namespace Worker.Models;

public record NextStepDecision(
    FinalAnswer? FinalAnswer,
    string? NeedUserQuestion,
    List<McpCall> ToolCalls);
