using Worker.Models.Content;

namespace Worker.Models;

public record NextStepDecision(
    FinalAnswer? FinalAnswer,
    string? NeedUserQuestion,
    List<AocFunctionCallContent> ToolCalls);
