using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using Serilog;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

/// <summary>
/// Static conversion methods between Microsoft.Extensions.AI types and OpenAI API formats
/// </summary>
public static class CustomOpenAiChatMessageConverter
{
    /// <summary>
    /// Converts IEnumerable<ChatMessage> to OpenAI message format
    /// </summary>
    public static List<OpenAiMessage> ToOpenAiMessages(IEnumerable<ChatMessage> messages)
    {
        var result = new List<OpenAiMessage>();

        foreach (var message in messages)
        {
            var openAiMessage = new OpenAiMessage
            {
                Role = MapChatRole(message.Role),
                Name = message.AuthorName
            };

            // Handle content conversion
            var contents = message.Contents.ToList();
            if (contents.Count == 0)
            {
                openAiMessage.Content = string.Empty;
            }
            else if (contents.Count == 1)
            {
                openAiMessage.Content = ConvertSingleContent(contents[0], message, out var toolCallId, out var toolCalls);
                if (toolCallId != null)
                {
                    openAiMessage.ToolCallId = toolCallId;
                }
                if (toolCalls != null)
                {
                    openAiMessage.ToolCalls = toolCalls;
                }
            }
            else
            {
                // Multiple contents - convert to array format
                openAiMessage.Content = ConvertMultipleContents(contents, message, out var toolCalls);
                if (toolCalls != null && toolCalls.Count > 0)
                {
                    openAiMessage.ToolCalls = toolCalls;
                }
            }

            result.Add(openAiMessage);
        }

        return result;
    }

    /// <summary>
    /// Maps ChatRole to OpenAI role string
    /// </summary>
    private static string MapChatRole(ChatRole role)
    {
        return role.Value switch
        {
            _ when role == ChatRole.System => "system",
            _ when role == ChatRole.User => "user",
            _ when role == ChatRole.Assistant => "assistant",
            _ when role == ChatRole.Tool => "tool",
            _ => role.Value ?? "user"
        };
    }

    /// <summary>
    /// Converts a single AIContent to OpenAI format
    /// </summary>
    private static object? ConvertSingleContent(
        AIContent content,
        ChatMessage message,
        out string? toolCallId,
        out List<OpenAiToolCall>? toolCalls)
    {
        toolCallId = null;
        toolCalls = null;

        switch (content)
        {
            case TextContent textContent:
                return textContent.Text;

            case FunctionCallContent functionCall:
                // Serialize arguments to JSON string for OpenAI API
                var argsJson = functionCall.Arguments != null
                    ? JsonSerializer.Serialize(functionCall.Arguments)
                    : "{}";

                toolCalls = new List<OpenAiToolCall>
                {
                    new()
                    {
                        Id = functionCall.CallId,
                        Type = "function",
                        Function = new OpenAiFunctionCall
                        {
                            Name = functionCall.Name,
                            Arguments = argsJson
                        }
                    }
                };
                return null;

            case FunctionResultContent functionResult:
                toolCallId = functionResult.CallId;
                return functionResult.Result?.ToString() ?? string.Empty;

            case DataContent dataContent:
                if (dataContent.Uri != null)
                {
                    return new List<OpenAiContentPart>
                    {
                        new()
                        {
                            Type = "image_url",
                            ImageUrl = new OpenAiImageUrl
                            {
                                Url = dataContent.Uri.ToString()
                            }
                        }
                    };
                }
                return string.Empty;

            case UsageContent:
            case ErrorContent:
                // These are metadata, not sent as content
                return null;

            default:
                // For other content types, convert to string representation
                return content.ToString();
        }
    }

    /// <summary>
    /// Converts multiple AIContents to OpenAI array format
    /// </summary>
    private static List<OpenAiContentPart> ConvertMultipleContents(
        IList<AIContent> contents,
        ChatMessage message,
        out List<OpenAiToolCall>? toolCalls)
    {
        toolCalls = null;
        var parts = new List<OpenAiContentPart>();

        foreach (var content in contents)
        {
            switch (content)
            {
                case TextContent textContent:
                    parts.Add(new OpenAiContentPart
                    {
                        Type = "text",
                        Text = textContent.Text
                    });
                    break;

                case DataContent dataContent when dataContent.Uri != null:
                    parts.Add(new OpenAiContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new OpenAiImageUrl
                        {
                            Url = dataContent.Uri.ToString()
                        }
                    });
                    break;

                case FunctionCallContent functionCall:
                    toolCalls ??= new List<OpenAiToolCall>();
                    var argsJson = functionCall.Arguments != null
                        ? JsonSerializer.Serialize(functionCall.Arguments)
                        : "{}";
                    toolCalls.Add(new OpenAiToolCall
                    {
                        Id = functionCall.CallId,
                        Type = "function",
                        Function = new OpenAiFunctionCall
                        {
                            Name = functionCall.Name,
                            Arguments = argsJson
                        }
                    });
                    break;

                case FunctionResultContent:
                case UsageContent:
                case ErrorContent:
                    // Skip metadata content types
                    break;
            }
        }

        return parts;
    }

    /// <summary>
    /// Applies ChatOptions to the OpenAI request
    /// </summary>
    public static void ApplyChatOptions(OpenAiChatCompletionRequest request, ChatOptions? options)
    {
        if (options == null) return;

        // Apply temperature
        if (options.AdditionalProperties?.TryGetValue("temperature", out var tempValue) == true ||
            options.AdditionalProperties?.TryGetValue("temperature_f", out tempValue) == true)
        {
            if (tempValue is float tempFloat)
            {
                request.Temperature = tempFloat;
            }
            else if (tempValue is double tempDouble)
            {
                request.Temperature = (float)tempDouble;
            }
        }

        // Apply max_tokens
        if (options.AdditionalProperties?.TryGetValue("max_tokens", out var maxTokensValue) == true ||
            options.AdditionalProperties?.TryGetValue("max_completion_tokens", out maxTokensValue) == true)
        {
            if (maxTokensValue is int maxTokensInt)
            {
                request.MaxTokens = maxTokensInt;
            }
        }

        // Apply top_p
        if (options.AdditionalProperties?.TryGetValue("top_p", out var topPValue) == true)
        {
            if (topPValue is float topPFloat)
            {
                request.TopP = topPFloat;
            }
            else if (topPValue is double topPDouble)
            {
                request.TopP = (float)topPDouble;
            }
        }

        // Apply stop
        if (options.AdditionalProperties?.TryGetValue("stop", out var stopValue) == true)
        {
            request.Stop = stopValue;
        }

        // Apply presence_penalty
        if (options.AdditionalProperties?.TryGetValue("presence_penalty", out var presencePenaltyValue) == true)
        {
            if (presencePenaltyValue is float presencePenaltyFloat)
            {
                request.PresencePenalty = presencePenaltyFloat;
            }
            else if (presencePenaltyValue is double presencePenaltyDouble)
            {
                request.PresencePenalty = (float)presencePenaltyDouble;
            }
        }

        // Apply frequency_penalty
        if (options.AdditionalProperties?.TryGetValue("frequency_penalty", out var frequencyPenaltyValue) == true)
        {
            if (frequencyPenaltyValue is float frequencyPenaltyFloat)
            {
                request.FrequencyPenalty = frequencyPenaltyFloat;
            }
            else if (frequencyPenaltyValue is double frequencyPenaltyDouble)
            {
                request.FrequencyPenalty = (float)frequencyPenaltyDouble;
            }
        }

        // Apply seed
        if (options.AdditionalProperties?.TryGetValue("seed", out var seedValue) == true)
        {
            if (seedValue is int seedInt)
            {
                request.Seed = seedInt;
            }
        }

        // Apply user
        if (options.AdditionalProperties?.TryGetValue("user", out var userValue) == true)
        {
            request.User = userValue?.ToString();
        }

        // Apply tools
        if (options.Tools != null && options.Tools.Count > 0)
        {
            request.Tools = ConvertTools(options.Tools);
        }

        // Apply tool_choice
        if (options.AdditionalProperties?.TryGetValue("tool_choice", out var toolChoiceValue) == true)
        {
            request.ToolChoice = toolChoiceValue;
        }
        else if (options.Tools != null && options.Tools.Count > 0)
        {
            // Default to auto when tools are present
            request.ToolChoice = "auto";
        }

        // Apply parallel_tool_calls
        if (options.AdditionalProperties?.TryGetValue("parallel_tool_calls", out var parallelToolCallsValue) == true)
        {
            if (parallelToolCallsValue is bool parallelToolCallsBool)
            {
                request.ParallelToolCalls = parallelToolCallsBool;
            }
        }
    }

    /// <summary>
    /// Converts AIFunctionDeclaration tools to OpenAI format
    /// </summary>
    private static List<OpenAiTool> ConvertTools(IList<AITool> tools)
    {
        var result = new List<OpenAiTool>();

        foreach (var tool in tools)
        {
            if (tool is AIFunctionDeclaration functionDeclaration)
            {
                // Try to get parameters from AdditionalProperties
                object? parameters = null;
                if (functionDeclaration.AdditionalProperties != null)
                {
                    if (functionDeclaration.AdditionalProperties.TryGetValue("parameters", out var paramsValue))
                    {
                        parameters = paramsValue;
                    }
                    else if (functionDeclaration.AdditionalProperties.TryGetValue("schema", out var schemaValue))
                    {
                        parameters = schemaValue;
                    }
                    else if (functionDeclaration.AdditionalProperties.TryGetValue("jsonSchema", out var jsonSchemaValue))
                    {
                        parameters = JsonSerializer.Deserialize<object>(
                            JsonSerializer.Serialize(jsonSchemaValue));
                    }
                }

                result.Add(new OpenAiTool
                {
                    Type = "function",
                    Function = new OpenAiFunctionDefinition
                    {
                        Name = functionDeclaration.Name,
                        Description = functionDeclaration.Description,
                        Parameters = parameters
                    }
                });
            }
        }

        return result;
    }

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
    /// Converts OpenAI message to list of AIContent
    /// </summary>
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
            Log.Verbose("[ToChatResponseUpdate] Processing {Count} tool calls in delta", delta.ToolCalls.Count);
            foreach (var toolCall in delta.ToolCalls)
            {
                toolCallBuilder?.AddChunk(toolCall);
            }
        }

        // Get accumulated tool calls if any are complete
        if (toolCallBuilder != null)
        {
            var completeCalls = toolCallBuilder.GetCompleteCalls();
            Log.Verbose("[ToChatResponseUpdate] Got {Count} complete calls from builder", completeCalls.Count);
            foreach (var call in completeCalls)
            {
                Log.Verbose("[ToChatResponseUpdate] Processing complete call - Id={Id}, Name={Name}, Args={Args}",
                    call.Id, call.Function.Name, call.Function.Arguments);

                Dictionary<string, object?>? argumentsDict = null;
                if (!string.IsNullOrEmpty(call.Function.Arguments))
                {
                    try
                    {
                        argumentsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Function.Arguments);
                        Log.Verbose("[ToChatResponseUpdate] Parsed arguments successfully: {ArgsDict}",
                            JsonSerializer.Serialize(argumentsDict));
                    }
                    catch (JsonException ex)
                    {
                        Log.Error(ex, "[ToChatResponseUpdate] Failed to parse arguments: {Args}", call.Function.Arguments);
                        argumentsDict = new Dictionary<string, object?>();
                    }
                }

                var functionCallContent = new FunctionCallContent(call.Id, call.Function.Name ?? string.Empty, argumentsDict ?? new Dictionary<string, object?>());
                Log.Verbose("[ToChatResponseUpdate] Created FunctionCallContent - CallId={CallId}, Name={Name}, ArgumentCount={ArgCount}",
                    functionCallContent.CallId, functionCallContent.Name, functionCallContent.Arguments?.Count ?? 0);
                contents.Add(functionCallContent);
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

    /// <summary>
    /// Maps OpenAI finish reason string to ChatFinishReason enum
    /// </summary>
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

    /// <summary>
    /// Builder for accumulating tool call data across streaming chunks
    /// </summary>
    public class OpenAiStreamToolCallBuilder
    {
        private readonly Dictionary<int, AccumulatingToolCall> _accumulatingCalls = new();
        private readonly HashSet<string> _emittedCallIds = new();

        /// <summary>
        /// Adds a tool call chunk to the accumulator
        /// </summary>
        public void AddChunk(OpenAiStreamToolCall chunk)
        {
            Log.Verbose("[ToolCallBuilder] AddChunk: Index={Index}, Id='{Id}', HasFunction={HasFunction}",
                chunk.Index, chunk.Id, chunk.Function != null);

            if (!_accumulatingCalls.TryGetValue(chunk.Index, out var call))
            {
                call = new AccumulatingToolCall { Index = chunk.Index };
                _accumulatingCalls[chunk.Index] = call;
                Log.Verbose("[ToolCallBuilder] Created new AccumulatingToolCall for index {Index}", chunk.Index);
            }

            if (!string.IsNullOrEmpty(chunk.Id))
            {
                call.Id = chunk.Id;
                Log.Verbose("[ToolCallBuilder] Set Id='{Id}' for index {Index}", chunk.Id, chunk.Index);
            }

            if (chunk.Function != null)
            {
                if (!string.IsNullOrEmpty(chunk.Function.Name))
                {
                    call.Name = chunk.Function.Name;
                    Log.Verbose("[ToolCallBuilder] Set Name='{Name}' for index {Index}", chunk.Function.Name, chunk.Index);
                }

                if (!string.IsNullOrEmpty(chunk.Function.Arguments))
                {
                    var beforeLength = call.ArgumentsBuilder.Length;
                    call.ArgumentsBuilder.Append(chunk.Function.Arguments);
                    Log.Verbose("[ToolCallBuilder] Appended arguments chunk: '{Chunk}' -> TotalLength={TotalLength}, CurrentArgs='{CurrentArgs}'",
                        chunk.Function.Arguments, call.ArgumentsBuilder.Length, call.ArgumentsBuilder.ToString());
                }
            }
        }

        /// <summary>
        /// Gets complete tool calls that have all required data
        /// </summary>
        public List<OpenAiToolCall> GetCompleteCalls()
        {
            Log.Verbose("[ToolCallBuilder] GetCompleteCalls called - Checking {Count} accumulating calls", _accumulatingCalls.Count);
            var complete = new List<OpenAiToolCall>();

            foreach (var kvp in _accumulatingCalls)
            {
                var call = kvp.Value;
                var arguments = call.ArgumentsBuilder.ToString();

                Log.Verbose("[ToolCallBuilder] Checking call Index={Index}, Id='{Id}', Name='{Name}', ArgsLength={ArgsLength}, Args='{Args}'",
                    call.Index, call.Id, call.Name, call.ArgumentsBuilder.Length, arguments);

                // A call is considered complete if it has id, name, and VALID (complete) JSON arguments
                if (!string.IsNullOrEmpty(call.Id) &&
                    !string.IsNullOrEmpty(call.Name) &&
                    call.ArgumentsBuilder.Length > 0)
                {
                    Log.Verbose("[ToolCallBuilder] Call has Id+Name+ArgsLength>0, checking if already emitted: {AlreadyEmitted}",
                        _emittedCallIds.Contains(call.Id));

                    var isValid = IsValidJson(arguments);
                    Log.Verbose("[ToolCallBuilder] IsValidJson returned: {IsValid} for args '{Args}'", isValid, arguments);

                    // Only return if not already emitted AND arguments form valid JSON
                    if (!_emittedCallIds.Contains(call.Id) && isValid)
                    {
                        Log.Verbose("[ToolCallBuilder] *** EMITTING CALL *** Id='{Id}', Name='{Name}', Args='{Args}'",
                            call.Id, call.Name, arguments);

                        _emittedCallIds.Add(call.Id); // Mark as emitted only when actually returning

                        complete.Add(new OpenAiToolCall
                        {
                            Index = call.Index,
                            Id = call.Id,
                            Type = "function",
                            Function = new OpenAiFunctionCall
                            {
                                Name = call.Name,
                                Arguments = arguments
                            }
                        });
                    }
                    else
                    {
                        Log.Verbose("[ToolCallBuilder] Call NOT emitted - AlreadyEmitted={AlreadyEmitted}, IsValid={IsValid}",
                            _emittedCallIds.Contains(call.Id), isValid);
                    }
                }
                else
                {
                    Log.Verbose("[ToolCallBuilder] Call incomplete - HasId={HasId}, HasName={HasName}, HasArgs={HasArgs}",
                        !string.IsNullOrEmpty(call.Id), !string.IsNullOrEmpty(call.Name), call.ArgumentsBuilder.Length > 0);
                }
            }

            Log.Verbose("[ToolCallBuilder] Returning {Count} complete calls", complete.Count);
            return complete;
        }

        /// <summary>
        /// Gets all accumulated tool calls, including incomplete ones
        /// </summary>
        public List<OpenAiToolCall> GetAllCalls(bool includeIncomplete = false)
        {
            Log.Verbose("[ToolCallBuilder] GetAllCalls called - includeIncomplete={IncludeIncomplete}", includeIncomplete);

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
                    Log.Verbose("[ToolCallBuilder] GetAllCalls adding call - Index={Index}, Id='{Id}', Name='{Name}', Args='{Args}'",
                        call.Index, call.Id, call.Name, call.ArgumentsBuilder.ToString());

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
            Log.Verbose("[ToolCallBuilder] Clear called - clearing {Count} accumulating calls and {EmittedCount} emitted IDs",
                _accumulatingCalls.Count, _emittedCallIds.Count);
            _accumulatingCalls.Clear();
            _emittedCallIds.Clear();
        }

        /// <summary>
        /// Checks if the accumulated arguments form complete, valid JSON
        /// </summary>
        private static bool IsValidJson(string json)
        {
            Log.Verbose("[ToolCallBuilder] IsValidJson checking: '{Json}'", json);

            if (string.IsNullOrWhiteSpace(json))
            {
                Log.Verbose("[ToolCallBuilder] IsValidJson: FALSE - null or whitespace");
                return false;
            }

            // Quick check: valid JSON object must start with { and end with }
            var trimmed = json.Trim();
            var startsEndsOk = trimmed.StartsWith('{') && trimmed.EndsWith('}');
            Log.Verbose("[ToolCallBuilder] IsValidJson: Quick structural check: {Ok} (starts with '{{': {Starts}, ends with '}}': {Ends})",
                startsEndsOk, trimmed.StartsWith('{'), trimmed.EndsWith('}'));

            if (!startsEndsOk)
            {
                Log.Verbose("[ToolCallBuilder] IsValidJson: FALSE - structural check failed");
                return false;
            }

            try
            {
                using var document = JsonDocument.Parse(json);
                var isObject = document.RootElement.ValueKind == JsonValueKind.Object;
                Log.Verbose("[ToolCallBuilder] IsValidJson: {Result} - parsed successfully, ValueKind={ValueKind}",
                    isObject, document.RootElement.ValueKind);
                return isObject;
            }
            catch (JsonException ex)
            {
                Log.Error("[ToolCallBuilder] IsValidJson: FALSE - JsonException: {Message}", ex.Message);
                return false;
            }
        }

        private class AccumulatingToolCall
        {
            public int Index { get; set; }
            public string Id { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public StringBuilder ArgumentsBuilder { get; } = new();
        }
    }
}
