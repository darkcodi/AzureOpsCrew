using Worker.Models.Content;

namespace Worker.Models;

public record LlmOutput(
    FinalAnswer? FinalAnswer,
    List<AocFunctionCallContent> ToolCalls);
