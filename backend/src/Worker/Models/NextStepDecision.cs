namespace Worker.Models;

public record NextStepDecision(
    FinalAnswer? FinalAnswer,
    ToolCallsRequest? ToolCallsRequest);
