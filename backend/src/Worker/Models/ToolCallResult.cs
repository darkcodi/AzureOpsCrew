namespace Worker.Models;

// ToDo: Rework this to include more details, such as tool call output, error message, etc.
public record ToolCallResult(string SerializedResult, bool IsError);
