using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

public static class OpenAiRequestMapper
{
    /// <summary>
    /// Creates an OpenAI chat completion request from messages and options
    /// </summary>
    public static OpenAiChatCompletionRequest MapToOpenAiRequest(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        string model,
        bool stream)
    {
        var openAiMessages = ToOpenAiMessages(messages);

        var request = new OpenAiChatCompletionRequest
        {
            Model = model,
            Messages = openAiMessages,
            Stream = stream
        };

        // Apply instructions (system prompt override)
        if (!string.IsNullOrEmpty(options?.Instructions))
        {
            // Check if there's already a system message
            var existingSystemMessage = request.Messages.FirstOrDefault(m => m.Role == "system");
            if (existingSystemMessage != null)
            {
                // Append instructions to existing system message
                var existingContent = existingSystemMessage.Content?.ToString() ?? string.Empty;
                existingSystemMessage.Content = $"{options.Instructions}\n\n{existingContent}";
            }
            else
            {
                // Prepend system message
                request.Messages.Insert(0, new OpenAiMessage
                {
                    Role = "system",
                    Content = options.Instructions
                });
            }
        }

        // Apply ChatOptions to request
        ApplyChatOptions(request, options);

        return request;
    }

    private static List<OpenAiMessage> ToOpenAiMessages(IEnumerable<ChatMessage> messages)
    {
        var normalizedMessages = NormalizeToolMessageOrder(messages);
        var result = new List<OpenAiMessage>();

        foreach (var message in normalizedMessages)
        {
            var mappedRole = MapChatRole(message.Role);
            var messageContents = message.Contents.ToList();

            // Check if this message has both reasoning and function calls
            var hasReasoning = messageContents.OfType<TextReasoningContent>().Any();
            var hasFunctionCalls = messageContents.OfType<FunctionCallContent>().Any();
            var hasFunctionResults = messageContents.OfType<FunctionResultContent>().Any();

            // Handle assistant messages that need to be split for DeepSeek format
            if (mappedRole == "assistant" && messageContents.Count > 1)
            {
                if (hasReasoning && hasFunctionCalls)
                {
                    // Create assistant message with reasoning + tool_calls (DeepSeek format)
                    var reasoningContent = messageContents.OfType<TextReasoningContent>().FirstOrDefault();
                    var functionCalls = messageContents.OfType<FunctionCallContent>().ToList();

                    var assistantMessage = new OpenAiMessage
                    {
                        Role = mappedRole,
                        Name = message.AuthorName,
                        ReasoningContent = reasoningContent?.Text
                    };

                    var toolCalls = new List<OpenAiToolCall>();
                    foreach (var functionCall in functionCalls)
                    {
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
                    }
                    assistantMessage.ToolCalls = toolCalls;

                    result.Add(assistantMessage);

                    // Create separate tool messages for function results
                    foreach (var functionResult in messageContents.OfType<FunctionResultContent>())
                    {
                        result.Add(new OpenAiMessage
                        {
                            Role = "tool",
                            ToolCallId = functionResult.CallId,
                            Content = functionResult.Result?.ToString() ?? string.Empty
                        });
                    }

                    continue;
                }

                // Handle messages with function results that should be separate tool messages
                if (hasFunctionResults)
                {
                    var hasOtherContent = messageContents.Any(c => c is not FunctionResultContent and not UsageContent and not ErrorContent);

                    if (!hasOtherContent)
                    {
                        // Only function results and metadata - create separate tool messages
                        foreach (var functionResult in messageContents.OfType<FunctionResultContent>())
                        {
                            result.Add(new OpenAiMessage
                            {
                                Role = "tool",
                                ToolCallId = functionResult.CallId,
                                Content = functionResult.Result?.ToString() ?? string.Empty
                            });
                        }
                        continue;
                    }
                }
            }

            // Default handling for other cases
            var openAiMessage = new OpenAiMessage
            {
                Role = mappedRole,
                Name = mappedRole == "tool" ? null : message.AuthorName
            };

            // Handle content conversion
            if (messageContents.Count == 0)
            {
                openAiMessage.Content = string.Empty;
            }
            else if (messageContents.Count == 1)
            {
                var content = messageContents[0];
                if (content is TextReasoningContent)
                {
                    openAiMessage.ReasoningContent = ConvertSingleContent(messageContents[0], out var toolCallId, out var toolCalls)?.ToString();
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
                    openAiMessage.Content = ConvertSingleContent(messageContents[0], out var toolCallId, out var toolCalls);
                    if (toolCallId != null)
                    {
                        openAiMessage.ToolCallId = toolCallId;
                    }
                    if (toolCalls != null)
                    {
                        openAiMessage.ToolCalls = toolCalls;
                    }
                }
            }
            else
            {
                // Multiple contents - convert to array format
                openAiMessage.Content = ConvertMultipleContents(messageContents, out var toolCalls);

                if (hasReasoning)
                {
                    var reasoningContent = messageContents.OfType<TextReasoningContent>().First();
                    openAiMessage.ReasoningContent = reasoningContent.Text;
                }

                if (toolCalls != null && toolCalls.Count > 0)
                {
                    openAiMessage.ToolCalls = toolCalls;
                }
            }

            if (openAiMessage.Content != null || openAiMessage.ReasoningContent != null || openAiMessage.ToolCalls != null)
            {
                // Edge case. Sometimes DeepSeek may produce messages with reasoning but no content or tool calls, which OpenAI API does not accept.
                if (openAiMessage.ReasoningContent != null &&
                    openAiMessage.Content == null &&
                    openAiMessage.ToolCalls == null)
                {
                    openAiMessage.Content = "[MISSING]";
                }

                // Only add messages that have content or tool calls to avoid sending empty messages to OpenAI
                result.Add(openAiMessage);
            }
        }

        return result;
    }

    private static List<ChatMessage> NormalizeToolMessageOrder(IEnumerable<ChatMessage> messages)
    {
        var orderedMessages = messages.ToList();
        var toolResultIndexesByCallId = BuildToolResultIndexesByCallId(orderedMessages);
        var emittedToolMessageIndexes = new HashSet<int>();
        var normalizedMessages = new List<ChatMessage>(orderedMessages.Count);

        for (var index = 0; index < orderedMessages.Count; index++)
        {
            if (emittedToolMessageIndexes.Contains(index))
            {
                continue;
            }

            var message = orderedMessages[index];
            normalizedMessages.Add(message);

            foreach (var toolCallId in GetToolCallIds(message))
            {
                if (!toolResultIndexesByCallId.TryGetValue(toolCallId, out var toolResultIndexes))
                {
                    continue;
                }

                foreach (var toolResultIndex in toolResultIndexes)
                {
                    if (toolResultIndex <= index || emittedToolMessageIndexes.Contains(toolResultIndex))
                    {
                        continue;
                    }

                    normalizedMessages.Add(orderedMessages[toolResultIndex]);
                    emittedToolMessageIndexes.Add(toolResultIndex);
                }
            }
        }

        return normalizedMessages;
    }

    private static Dictionary<string, List<int>> BuildToolResultIndexesByCallId(IReadOnlyList<ChatMessage> messages)
    {
        var result = new Dictionary<string, List<int>>(StringComparer.Ordinal);

        for (var index = 0; index < messages.Count; index++)
        {
            foreach (var toolCallId in GetToolResultCallIds(messages[index]))
            {
                if (!result.TryGetValue(toolCallId, out var indexes))
                {
                    indexes = [];
                    result[toolCallId] = indexes;
                }

                indexes.Add(index);
            }
        }

        return result;
    }

    private static IEnumerable<string> GetToolCallIds(ChatMessage message)
    {
        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent functionCall &&
                !string.IsNullOrWhiteSpace(functionCall.CallId))
            {
                yield return functionCall.CallId;
            }
        }
    }

    private static IEnumerable<string> GetToolResultCallIds(ChatMessage message)
    {
        if (message.Role != ChatRole.Tool)
        {
            yield break;
        }

        foreach (var content in message.Contents)
        {
            if (content is FunctionResultContent functionResult &&
                !string.IsNullOrWhiteSpace(functionResult.CallId))
            {
                yield return functionResult.CallId;
            }
        }
    }

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

    private static object? ConvertSingleContent(
        AIContent content,
        out string? toolCallId,
        out List<OpenAiToolCall>? toolCalls)
    {
        toolCallId = null;
        toolCalls = null;

        switch (content)
        {
            case TextContent textContent:
                return textContent.Text;

            case TextReasoningContent textReasoningContent:
                return textReasoningContent.Text;

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

    private static List<OpenAiContentPart>? ConvertMultipleContents(
        IList<AIContent> contents,
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

        if (parts.Count == 0)
        {
            return null;
        }
        return parts;
    }

    private static void ApplyChatOptions(OpenAiChatCompletionRequest request, ChatOptions? options)
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

    private static List<OpenAiTool> ConvertTools(IList<AITool> tools)
    {
        var result = new List<OpenAiTool>();

        foreach (var tool in tools)
        {
            if (tool is AIFunctionDeclaration functionDeclaration)
            {
                // Get parameters from the JsonSchema property (set by AIFunctionFactory.CreateDeclaration)
                object? parameters = null;
                if (functionDeclaration.JsonSchema.ValueKind != JsonValueKind.Undefined)
                {
                    parameters = JsonSerializer.Deserialize<object>(JsonSerializer.Serialize(functionDeclaration.JsonSchema));
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
}
