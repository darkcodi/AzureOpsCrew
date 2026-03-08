using System.Text.Json;
using AzureOpsCrew.Domain.Tools;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.ContextReduction;

public static class TokenEstimator
{
    private const int MessageOverheadTokens = 4;

    public static int EstimateTokens(string? text, double charsPerToken, double safetyMargin)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        return (int)Math.Ceiling(text.Length / charsPerToken * safetyMargin);
    }

    public static int EstimateMessageTokens(ChatMessage message, double charsPerToken, double safetyMargin)
    {
        var tokens = MessageOverheadTokens;

        foreach (var content in message.Contents)
        {
            tokens += EstimateContentTokens(content, charsPerToken, safetyMargin);
        }

        return tokens;
    }

    public static int EstimateMessagesTokens(IReadOnlyList<ChatMessage> messages, double charsPerToken, double safetyMargin)
    {
        var total = 0;
        foreach (var message in messages)
        {
            total += EstimateMessageTokens(message, charsPerToken, safetyMargin);
        }
        return total;
    }

    public static int EstimateSystemPromptTokens(string? systemPrompt, double charsPerToken, double safetyMargin)
    {
        return EstimateTokens(systemPrompt, charsPerToken, safetyMargin);
    }

    public static int EstimateToolSchemasTokens(IReadOnlyList<ToolDeclaration> tools, double charsPerToken, double safetyMargin)
    {
        var total = 0;
        foreach (var tool in tools)
        {
            total += EstimateTokens(tool.Name, charsPerToken, safetyMargin);
            total += EstimateTokens(tool.Description, charsPerToken, safetyMargin);
            total += EstimateTokens(tool.JsonSchema, charsPerToken, safetyMargin);
            total += EstimateTokens(tool.ReturnJsonSchema, charsPerToken, safetyMargin);
        }
        return total;
    }

    private static int EstimateContentTokens(AIContent content, double charsPerToken, double safetyMargin)
    {
        switch (content)
        {
            case TextContent tc:
                return EstimateTokens(tc.Text, charsPerToken, safetyMargin);

            case TextReasoningContent trc:
                return EstimateTokens(trc.Text, charsPerToken, safetyMargin);

            case FunctionCallContent fcc:
            {
                var tokens = EstimateTokens(fcc.Name, charsPerToken, safetyMargin);
                tokens += EstimateTokens(fcc.CallId, charsPerToken, safetyMargin);
                if (fcc.Arguments != null)
                {
                    var argsJson = JsonSerializer.Serialize(fcc.Arguments);
                    tokens += EstimateTokens(argsJson, charsPerToken, safetyMargin);
                }
                return tokens;
            }

            case FunctionResultContent frc:
            {
                var tokens = EstimateTokens(frc.CallId, charsPerToken, safetyMargin);
                if (frc.Result != null)
                {
                    var resultStr = frc.Result is string s ? s : JsonSerializer.Serialize(frc.Result);
                    tokens += EstimateTokens(resultStr, charsPerToken, safetyMargin);
                }
                return tokens;
            }

            default:
                return EstimateTokens(content.ToString(), charsPerToken, safetyMargin);
        }
    }
}
