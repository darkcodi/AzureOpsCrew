using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.Models.Content;

public sealed class AocFunctionResultContent : AocAiContent
{
    public string CallId { get; set; } = string.Empty;
    public object? Result { get; set; }

    public static AocFunctionResultContent ToolDoesNotExist(string callId)
    {
        return new AocFunctionResultContent
        {
            CallId = callId,
            Result = new ToolCallResult(SerializedResult: JsonDocument.Parse("{\"ErrorMessage\":\"Tool does not exist\"}").RootElement.ToString(), IsError: true),
        };
    }

    public static AocFunctionResultContent Empty(string callId)
    {
        return new AocFunctionResultContent
        {
            CallId = callId,
            Result = new ToolCallResult(SerializedResult: JsonDocument.Parse("{}").RootElement.ToString(), IsError: false),
        };
    }
}
