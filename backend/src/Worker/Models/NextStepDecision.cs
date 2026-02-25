using Worker.Models.Content;

namespace Worker.Models;

public record NextStepDecision(
    FinalAnswer? FinalAnswer,
    List<AocFunctionCallContent> ToolCalls);
