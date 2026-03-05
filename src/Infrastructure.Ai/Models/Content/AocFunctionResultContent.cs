using System.Text.Json;
using AzureOpsCrew.Domain.Tools;

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
            Result = new ToolCallResult(SerializedResult: "{\"ErrorMessage\":\"Tool does not exist\"}", IsError: true),
        };
    }

    public static AocFunctionResultContent Empty(string callId)
    {
        return new AocFunctionResultContent
        {
            CallId = callId,
            Result = new ToolCallResult(SerializedResult: "{}", IsError: false),
        };
    }
}
