using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

public static class OpenAiResponseMapper
{
    /// <summary>
    /// Converts OpenAI response to ChatResponse
    /// </summary>
    public static ChatResponse ToChatResponse(OpenAiChatCompletionResponse openAiResponse)
    {
        if (openAiResponse.Choices.Count == 0)
        {
            throw new InvalidOperationException("OpenAI response contained no choices");
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
            if (response.AdditionalProperties == null)
            {
                response.AdditionalProperties = new AdditionalPropertiesDictionary();
            }
            response.AdditionalProperties["finish_reason"] = choice.FinishReason;
        }

        return response;
    }

    /// <summary>
    /// Converts OpenAI streaming chunk to ChatResponseUpdate
    /// </summary>
    public static ChatResponseUpdate ToChatResponseUpdate(
        OpenAiChatCompletionChunk chunk,
        ref bool isReasoning,
        OpenAiStreamToolCallBuilder? toolCallBuilder = null)
    {
        if (chunk.Choices.Count == 0)
        {
            return new ChatResponseUpdate(ChatRole.Assistant, new List<AIContent>());
        }

        var choice = chunk.Choices[0];
        var delta = choice.Delta;
        var contents = new List<AIContent>();

        // Add text content if present
        if (!string.IsNullOrEmpty(delta.Content))
        {
            if (delta.Content.Contains("<think>") ||
                delta.Content.Contains("<thinking>") ||
                delta.Content.Contains("<reason>") ||
                delta.Content.Contains("<reasoning>"))
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

            if (delta.Content.Contains("</think>") ||
                delta.Content.Contains("</thinking>") ||
                delta.Content.Contains("</reason>") ||
                delta.Content.Contains("</reasoning>"))
            {
                isReasoning = false;
            }
        }
        else if (!string.IsNullOrEmpty(delta.Reasoning))
        {
            contents.Add(new TextReasoningContent(delta.Reasoning));
        }

        // Handle tool calls in streaming
        if (delta.ToolCalls != null && delta.ToolCalls.Count > 0)
        {
            foreach (var toolCall in delta.ToolCalls)
            {
                toolCallBuilder?.AddChunk(toolCall);
            }
        }

        // Get accumulated tool calls if any are complete
        if (toolCallBuilder != null)
        {
            var completeCalls = toolCallBuilder.GetCompleteCalls();
            foreach (var call in completeCalls)
            {
                Dictionary<string, object?>? argumentsDict = null;
                if (!string.IsNullOrEmpty(call.Function.Arguments))
                {
                    try
                    {
                        argumentsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Function.Arguments);
                    }
                    catch (JsonException)
                    {
                        argumentsDict = new Dictionary<string, object?>();
                    }
                }

                contents.Add(new FunctionCallContent(call.Id, call.Function.Name ?? string.Empty, argumentsDict ?? new Dictionary<string, object?>()));
            }
        }

        // Add usage if present (usually in last chunk) - before creating update
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

        var update = new ChatResponseUpdate(ChatRole.Assistant, contents)
        {
            MessageId = chunk.Id,
            ModelId = chunk.Model
        };

        // Add finish reason if present
        if (!string.IsNullOrEmpty(choice.FinishReason))
        {
            update.FinishReason = MapFinishReason(choice.FinishReason);
        }

        return update;
    }

    private static ChatFinishReason MapFinishReason(string finishReason)
        {
            return finishReason.ToLowerInvariant() switch
            {
                "stop" => ChatFinishReason.Stop,
                "length" => ChatFinishReason.Length,
                "tool_calls" => ChatFinishReason.ToolCalls,
                "content_filter" => ChatFinishReason.ContentFilter,
                _ => ChatFinishReason.Stop
            };
        }

    private static List<AIContent> ConvertMessageToContents(OpenAiMessage message)
    {
        var contents = new List<AIContent>();

        // Add text content if present
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
                // Handle JSON content
                if (element.ValueKind == JsonValueKind.String)
                {
                    contents.Add(new TextContent(element.GetString() ?? string.Empty));
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    // Multi-content format
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.TryGetProperty("type", out var type))
                        {
                            var typeValue = type.GetString();
                            if (typeValue == "text" && item.TryGetProperty("text", out var textProperty))
                            {
                                contents.Add(new TextContent(textProperty.GetString() ?? string.Empty));
                            }
                            else if (typeValue == "image_url" && item.TryGetProperty("image_url", out var imageUrlProp))
                            {
                                if (imageUrlProp.TryGetProperty("url", out var urlProp))
                                {
                                    contents.Add(new DataContent(new Uri(urlProp.GetString() ?? string.Empty)));
                                }
                            }
                        }
                    }
                }
            }
        }

        // Add tool calls if present
        if (message.ToolCalls != null)
        {
            foreach (var toolCall in message.ToolCalls)
            {
                // Parse JSON arguments to dictionary
                Dictionary<string, object?>? argumentsDict = null;
                if (!string.IsNullOrEmpty(toolCall.Function.Arguments))
                {
                    try
                    {
                        argumentsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCall.Function.Arguments);
                    }
                    catch (JsonException)
                    {
                        // If parsing fails, create empty dict
                        argumentsDict = new Dictionary<string, object?>();
                    }
                }

                contents.Add(new FunctionCallContent(
                    toolCall.Id,
                    toolCall.Function.Name ?? string.Empty,
                    argumentsDict ?? new Dictionary<string, object?>()));
            }
        }

        return contents;
    }
}

public class OpenAiStreamToolCallBuilder
{
    private readonly Dictionary<int, AccumulatingToolCall> _accumulatingCalls = new();
    private readonly HashSet<string> _emittedCallIds = new();

    /// <summary>
    /// Adds a tool call chunk to the accumulator
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
    /// Gets complete tool calls that have all required data
    /// </summary>
    public List<OpenAiToolCall> GetCompleteCalls()
    {
        var complete = new List<OpenAiToolCall>();

        foreach (var kvp in _accumulatingCalls)
        {
            var call = kvp.Value;
            // A call is considered complete if it has id, name, and arguments
            if (!string.IsNullOrEmpty(call.Id) &&
                !string.IsNullOrEmpty(call.Name) &&
                call.ArgumentsBuilder.Length > 0)
            {
                // Only return if not already emitted
                if (_emittedCallIds.Add(call.Id))  // Add returns false if already exists
                {
                    complete.Add(new OpenAiToolCall
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
            }
        }

        return complete;
    }

    /// <summary>
    /// Gets all accumulated tool calls, including incomplete ones
    /// </summary>
    public List<OpenAiToolCall> GetAllCalls(bool includeIncomplete = false)
    {
        if (includeIncomplete)
        {
            return GetCompleteCalls();
        }

        var all = new List<OpenAiToolCall>();

        foreach (var kvp in _accumulatingCalls)
        {
            var call = kvp.Value;
            var hasData = !string.IsNullOrEmpty(call.Id) ||
                          !string.IsNullOrEmpty(call.Name) ||
                          call.ArgumentsBuilder.Length > 0;

            if (hasData)
            {
                all.Add(new OpenAiToolCall
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
        }

        return all;
    }

    /// <summary>
    /// Clears accumulated state
    /// </summary>
    public void Clear()
    {
        _accumulatingCalls.Clear();
        _emittedCallIds.Clear();
    }

    private class AccumulatingToolCall
    {
        public int Index { get; set; }
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public StringBuilder ArgumentsBuilder { get; } = new();
    }
}
