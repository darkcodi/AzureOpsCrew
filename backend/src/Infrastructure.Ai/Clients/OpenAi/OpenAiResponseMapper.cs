using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

public static class OpenAiResponseMapper
{
    /// <summary>
    /// Converts a non-streaming OpenAI response to ChatResponse.
    /// </summary>
    public static ChatResponse ToChatResponse(OpenAiChatCompletionResponse openAiResponse)
    {
        if (openAiResponse.Choices.Count == 0)
        {
            throw new InvalidOperationException("OpenAI response contained no choices.");
        }

        var choice = openAiResponse.Choices[0];
        var contents = ConvertMessageToContents(choice.Message);

        // Add usage information as content before creating the message
        if (openAiResponse.Usage != null)
        {
            contents.Add(new UsageContent(new UsageDetails
            {
                InputTokenCount = openAiResponse.Usage.PromptTokens,
                OutputTokenCount = openAiResponse.Usage.CompletionTokens,
                TotalTokenCount = openAiResponse.Usage.TotalTokens,
                CachedInputTokenCount = openAiResponse.Usage.PromptTokensDetails?.CachedTokens,
                ReasoningTokenCount = openAiResponse.Usage.CompletionTokensDetails?.ReasoningTokens
            }));
        }

        // Create ChatMessage first
        var chatMessage = new ChatMessage(ChatRole.Assistant, contents)
        {
            MessageId = openAiResponse.Id
        };

        // Create ChatResponse from ChatMessage
        var response = new ChatResponse(chatMessage)
        {
            ModelId = openAiResponse.Model,
            CreatedAt = DateTimeOffset.FromUnixTimeSeconds(openAiResponse.Created).DateTime
        };

        // Add finish reason to additional properties
        if (!string.IsNullOrEmpty(choice.FinishReason))
        {
            response.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            response.AdditionalProperties["finish_reason"] = choice.FinishReason;
        }

        return response;
    }

    /// <summary>
    /// Converts a streaming OpenAI chunk to ChatResponseUpdate.
    /// </summary>
    public static ChatResponseUpdate ToChatResponseUpdate(
        OpenAiChatCompletionChunk chunk,
        ref bool isReasoning,
        OpenAiStreamToolCallBuilder? toolCallBuilder = null)
    {
        var contents = new List<AIContent>();

        // Streaming usage can arrive in a chunk with choices: []
        if (chunk.Usage != null)
        {
            contents.Add(new UsageContent(new UsageDetails
            {
                InputTokenCount = chunk.Usage.PromptTokens,
                OutputTokenCount = chunk.Usage.CompletionTokens,
                TotalTokenCount = chunk.Usage.TotalTokens,
                CachedInputTokenCount = chunk.Usage.PromptTokensDetails?.CachedTokens,
                ReasoningTokenCount = chunk.Usage.CompletionTokensDetails?.ReasoningTokens
            }));
        }

        // Annotation / moderation / usage-only chunks can have no choices.
        if (chunk.Choices.Count == 0)
        {
            return new ChatResponseUpdate(ChatRole.Assistant, contents)
            {
                MessageId = chunk.Id,
                ModelId = chunk.Model
            };
        }

        var choice = chunk.Choices[0];
        var delta = choice.Delta;

        // Add text content if present
        if (!string.IsNullOrEmpty(delta.Content))
        {
            if (delta.Content.Contains("<think>", StringComparison.OrdinalIgnoreCase) ||
                delta.Content.Contains("<thinking>", StringComparison.OrdinalIgnoreCase) ||
                delta.Content.Contains("<reason>", StringComparison.OrdinalIgnoreCase) ||
                delta.Content.Contains("<reasoning>", StringComparison.OrdinalIgnoreCase))
            {
                isReasoning = true;
            }

            if (isReasoning)
            {
                contents.Add(new TextReasoningContent(delta.Content));
            }
            else
            {
                contents.Add(new TextContent(delta.Content));
            }

            if (delta.Content.Contains("</think>", StringComparison.OrdinalIgnoreCase) ||
                delta.Content.Contains("</thinking>", StringComparison.OrdinalIgnoreCase) ||
                delta.Content.Contains("</reason>", StringComparison.OrdinalIgnoreCase) ||
                delta.Content.Contains("</reasoning>", StringComparison.OrdinalIgnoreCase))
            {
                isReasoning = false;
            }
        }
        else if (!string.IsNullOrEmpty(delta.Reasoning))
        {
            contents.Add(new TextReasoningContent(delta.Reasoning));
        }

        // Accumulate streamed tool-call fragments.
        if (delta.ToolCalls != null && delta.ToolCalls.Count > 0)
        {
            foreach (var toolCall in delta.ToolCalls)
            {
                toolCallBuilder?.AddChunk(toolCall);
            }
        }

        // Finalize tool calls only when the model explicitly ends on tool_calls/function_call.
        if (toolCallBuilder != null &&
            IsToolCallFinishReason(choice.FinishReason))
        {
            foreach (var call in toolCallBuilder.GetFinalizableCalls())
            {
                var argumentsDict = ParseArgumentsToDictionary(
                    call.Function.Arguments,
                    throwOnInvalidJson: true);

                contents.Add(new FunctionCallContent(
                    call.Id,
                    call.Function.Name ?? string.Empty,
                    argumentsDict));
            }

            toolCallBuilder.Clear();
        }

        var update = new ChatResponseUpdate(ChatRole.Assistant, contents)
        {
            MessageId = chunk.Id,
            ModelId = chunk.Model
        };

        if (!string.IsNullOrEmpty(choice.FinishReason))
        {
            update.FinishReason = MapFinishReason(choice.FinishReason);
        }

        return update;
    }

    private static bool IsToolCallFinishReason(string? finishReason) =>
        string.Equals(finishReason, "tool_calls", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(finishReason, "function_call", StringComparison.OrdinalIgnoreCase);

    private static ChatFinishReason MapFinishReason(string finishReason)
    {
        return finishReason.ToLowerInvariant() switch
        {
            "stop" => ChatFinishReason.Stop,
            "length" => ChatFinishReason.Length,
            "tool_calls" => ChatFinishReason.ToolCalls,
            "function_call" => ChatFinishReason.ToolCalls,
            "content_filter" => ChatFinishReason.ContentFilter,
            _ => ChatFinishReason.Stop
        };
    }

    private static List<AIContent> ConvertMessageToContents(OpenAiMessage message)
    {
        var contents = new List<AIContent>();

        if (message.Content != null)
        {
            if (message.Content is string text)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    contents.Add(new TextContent(text));
                }
            }
            else if (message.Content is JsonElement element)
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var textValue = element.GetString();
                    if (!string.IsNullOrEmpty(textValue))
                    {
                        contents.Add(new TextContent(textValue));
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        if (!item.TryGetProperty("type", out var type))
                        {
                            continue;
                        }

                        var typeValue = type.GetString();

                        if (typeValue == "text" && item.TryGetProperty("text", out var textProperty))
                        {
                            var textContent = textProperty.GetString();
                            if (!string.IsNullOrEmpty(textContent))
                            {
                                contents.Add(new TextContent(textContent));
                            }
                        }
                        else if (typeValue == "image_url" &&
                                 item.TryGetProperty("image_url", out var imageUrlProp) &&
                                 imageUrlProp.TryGetProperty("url", out var urlProp))
                        {
                            var url = urlProp.GetString();
                            if (!string.IsNullOrEmpty(url))
                            {
                                contents.Add(new DataContent(new Uri(url)));
                            }
                        }
                    }
                }
            }
        }

        if (message.ToolCalls != null)
        {
            foreach (var toolCall in message.ToolCalls)
            {
                var argumentsDict = ParseArgumentsToDictionary(
                    toolCall.Function.Arguments,
                    throwOnInvalidJson: true);

                contents.Add(new FunctionCallContent(
                    toolCall.Id,
                    toolCall.Function.Name ?? string.Empty,
                    argumentsDict));
            }
        }

        return contents;
    }

    private static Dictionary<string, object?> ParseArgumentsToDictionary(
        string? argumentsJson,
        bool throwOnInvalidJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(argumentsJson);

            if (element.ValueKind == JsonValueKind.Null)
            {
                return new Dictionary<string, object?>();
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"Tool/function arguments must be a JSON object, but were {element.ValueKind}.");
            }

            return JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsJson)
                   ?? new Dictionary<string, object?>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            if (throwOnInvalidJson)
            {
                throw new InvalidOperationException(
                    $"Tool/function arguments were not valid JSON object: {argumentsJson}",
                    ex);
            }

            return new Dictionary<string, object?>();
        }
    }
}

public sealed class OpenAiStreamToolCallBuilder
{
    private readonly Dictionary<int, AccumulatingToolCall> _accumulatingCalls = new();

    /// <summary>
    /// Adds a streamed tool-call fragment to the accumulator.
    /// </summary>
    public void AddChunk(OpenAiStreamToolCall chunk)
    {
        if (!_accumulatingCalls.TryGetValue(chunk.Index, out var call))
        {
            call = new AccumulatingToolCall { Index = chunk.Index };
            _accumulatingCalls[chunk.Index] = call;
        }

        if (!string.IsNullOrEmpty(chunk.Id))
        {
            call.Id = chunk.Id;
        }

        if (chunk.Function != null)
        {
            if (!string.IsNullOrEmpty(chunk.Function.Name))
            {
                call.Name = chunk.Function.Name;
            }

            if (!string.IsNullOrEmpty(chunk.Function.Arguments))
            {
                call.ArgumentsBuilder.Append(chunk.Function.Arguments);
            }
        }
    }

    /// <summary>
    /// Returns tool calls that are ready to be finalized after a terminal tool-calls finish reason.
    /// This does not attempt to guess JSON completeness mid-stream.
    /// </summary>
    public List<OpenAiToolCall> GetFinalizableCalls()
    {
        var result = new List<OpenAiToolCall>();

        foreach (var call in _accumulatingCalls.Values.OrderBy(c => c.Index))
        {
            if (string.IsNullOrEmpty(call.Id) || string.IsNullOrEmpty(call.Name))
            {
                continue;
            }

            result.Add(new OpenAiToolCall
            {
                Index = call.Index,
                Id = call.Id,
                Type = "function",
                Function = new OpenAiFunctionCall
                {
                    Name = call.Name,
                    Arguments = call.ArgumentsBuilder.ToString()
                }
            });
        }

        return result;
    }

    /// <summary>
    /// Gets all accumulated tool calls. If includeIncomplete is false,
    /// only returns calls with at least id and name.
    /// </summary>
    public List<OpenAiToolCall> GetAllCalls(bool includeIncomplete = false)
    {
        var result = new List<OpenAiToolCall>();

        foreach (var call in _accumulatingCalls.Values.OrderBy(c => c.Index))
        {
            var isCompleteEnough =
                !string.IsNullOrEmpty(call.Id) &&
                !string.IsNullOrEmpty(call.Name);

            if (!includeIncomplete && !isCompleteEnough)
            {
                continue;
            }

            var hasAnyData =
                !string.IsNullOrEmpty(call.Id) ||
                !string.IsNullOrEmpty(call.Name) ||
                call.ArgumentsBuilder.Length > 0;

            if (!hasAnyData)
            {
                continue;
            }

            result.Add(new OpenAiToolCall
            {
                Index = call.Index,
                Id = call.Id ?? string.Empty,
                Type = "function",
                Function = new OpenAiFunctionCall
                {
                    Name = call.Name ?? string.Empty,
                    Arguments = call.ArgumentsBuilder.ToString()
                }
            });
        }

        return result;
    }

    /// <summary>
    /// Clears accumulated state.
    /// </summary>
    public void Clear()
    {
        _accumulatingCalls.Clear();
    }

    private sealed class AccumulatingToolCall
    {
        public int Index { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public StringBuilder ArgumentsBuilder { get; } = new();
    }
}
