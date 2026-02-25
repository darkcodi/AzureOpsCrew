using Worker.Models.Content;

namespace Worker.Models;

public record ToolCallsRequest(string? Text, List<AocFunctionCallContent> ToolCalls);
