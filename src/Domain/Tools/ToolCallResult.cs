namespace AzureOpsCrew.Domain.Tools;

// ToDo: Rework this to include more details, such as tool call output, error message, etc.
public record ToolCallResult(string CallId, object? Result, bool IsError);
