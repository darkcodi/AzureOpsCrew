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
        bool stream,
        OpenAiRequestProfile? profile = null)
    {
        var effectiveProfile = profile ?? OpenAiRequestProfile.Default;
        var openAiMessages = ToOpenAiMessages(messages, effectiveProfile);

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

    private static List<OpenAiMessage> ToOpenAiMessages(IEnumerable<ChatMessage> messages, OpenAiRequestProfile profile)
    {
        var normalizedMessages = NormalizeToolMessageOrder(messages);
        var result = new List<OpenAiMessage>();

        foreach (var message in normalizedMessages)
        {
            var mappedRole = MapChatRole(message.Role);
            var messageContents = message.Contents.ToList();

            // Handle assistant messages that need to be split for DeepSeek format
            if (mappedRole == "assistant" && messageContents.Count > 1)
            {
                // Check if this message has both reasoning and function calls
                var hasReasoning = messageContents.OfType<TextReasoningContent>().Any();
                var hasFunctionCalls = messageContents.OfType<FunctionCallContent>().Any();
                var hasFunctionResults = messageContents.OfType<FunctionResultContent>().Any();

                if (hasReasoning && hasFunctionCalls)
                {
                    // Create assistant message with reasoning + tool_calls (DeepSeek format)
                    var reasoningContent = string.Join(
                        "\n",
                        messageContents
                            .OfType<TextReasoningContent>()
                            .Select(r => r.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t)));
                    var functionCalls = messageContents.OfType<FunctionCallContent>().ToList();

                    var assistantMessage = new OpenAiMessage
                    {
                        Role = mappedRole,
                        Name = message.AuthorName,
                        ReasoningContent = profile.IncludeReasoningContent ? reasoningContent : null
                    };

                    var assistantText = string.Join(
                        "\n",
                        messageContents
                            .OfType<TextContent>()
                            .Select(t => t.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t)));
                    if (!string.IsNullOrWhiteSpace(assistantText))
                    {
                        assistantMessage.Content = assistantText;
                    }
                    else if (profile.InjectAssistantContentFromReasoningWhenMissing &&
                             !string.IsNullOrWhiteSpace(reasoningContent))
                    {
                        assistantMessage.Content = BuildReasoningFallbackContent(reasoningContent);
                    }

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

                if (hasReasoning && !hasFunctionCalls)
                {
                    var reasoningText = string.Join(
                        "\n",
                        messageContents
                            .OfType<TextReasoningContent>()
                            .Select(r => r.Text)
                            .Where(t => !string.IsNullOrWhiteSpace(t)));

                    var nonReasoningContents = messageContents
                        .Where(c => c is not TextReasoningContent)
                        .ToList();

                    var assistantMessage = new OpenAiMessage
                    {
                        Role = mappedRole,
                        Name = message.AuthorName,
                        ReasoningContent = profile.IncludeReasoningContent ? reasoningText : null
                    };

                    if (nonReasoningContents.Count == 0)
                    {
                        assistantMessage.Content = profile.InjectAssistantContentFromReasoningWhenMissing && !string.IsNullOrWhiteSpace(reasoningText)
                            ? BuildReasoningFallbackContent(reasoningText)
                            : null;
                    }
                    else if (nonReasoningContents.Count == 1)
                    {
                        assistantMessage.Content = ConvertSingleContent(nonReasoningContents[0], out _, out var toolCalls);
                        if (toolCalls != null && toolCalls.Count > 0)
                            assistantMessage.ToolCalls = toolCalls;
                    }
                    else
                    {
                        assistantMessage.Content = ConvertMultipleContents(nonReasoningContents, out var toolCalls);
                        if (toolCalls != null && toolCalls.Count > 0)
                            assistantMessage.ToolCalls = toolCalls;
                    }

                    if (ShouldKeepMessage(assistantMessage, profile))
                    {
                        result.Add(assistantMessage);
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
                    var reasoningText = ConvertSingleContent(messageContents[0], out var toolCallId, out var toolCalls)?.ToString();
                    if (profile.IncludeReasoningContent)
                    {
                        openAiMessage.ReasoningContent = reasoningText;
                    }
                    else
                    {
                        openAiMessage.Content = reasoningText;
                    }
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
                if (toolCalls != null && toolCalls.Count > 0)
                {
                    openAiMessage.ToolCalls = toolCalls;
                }
            }

            if (ShouldKeepMessage(openAiMessage, profile))
            {
                result.Add(openAiMessage);
            }
        }

        return NormalizeAndValidateMessagesForProfile(result, profile);
    }

    private static bool ShouldKeepMessage(OpenAiMessage message, OpenAiRequestProfile profile)
    {
        if (message.Role != "assistant")
        {
            if (message.Role == "tool")
            {
                return !string.IsNullOrWhiteSpace(message.ToolCallId) && message.Content != null;
            }

            return message.Content != null || !string.IsNullOrWhiteSpace(message.ReasoningContent);
        }

        if (!profile.RequireAssistantContentOrToolCalls)
        {
            return message.Content != null || !string.IsNullOrWhiteSpace(message.ReasoningContent) ||
                   (message.ToolCalls != null && message.ToolCalls.Count > 0);
        }

        if (HasAssistantPayload(message))
        {
            return true;
        }

        if (!profile.InjectAssistantContentFromReasoningWhenMissing ||
            string.IsNullOrWhiteSpace(message.ReasoningContent))
        {
            return false;
        }

        message.Content = BuildReasoningFallbackContent(message.ReasoningContent);
        return HasAssistantPayload(message);
    }

    private static List<OpenAiMessage> NormalizeAndValidateMessagesForProfile(
        IEnumerable<OpenAiMessage> messages,
        OpenAiRequestProfile profile)
    {
        var result = new List<OpenAiMessage>();
        var seenToolCallIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var message in messages)
        {
            if (message.Role == "assistant")
            {
                if (profile.IncludeReasoningContent &&
                    profile.RequireReasoningContentForAssistantMessages &&
                    string.IsNullOrWhiteSpace(message.ReasoningContent) &&
                    HasTextualAssistantContent(message))
                {
                    message.ReasoningContent = ExtractTextualContent(message.Content);
                }

                if (profile.RequireAssistantContentOrToolCalls && !HasAssistantPayload(message))
                {
                    if (!profile.InjectAssistantContentFromReasoningWhenMissing ||
                        string.IsNullOrWhiteSpace(message.ReasoningContent))
                    {
                        continue;
                    }

                    message.Content = BuildReasoningFallbackContent(message.ReasoningContent);
                }

                if (message.ToolCalls != null)
                {
                    foreach (var toolCall in message.ToolCalls.Where(t => !string.IsNullOrWhiteSpace(t.Id)))
                    {
                        seenToolCallIds.Add(toolCall.Id);
                    }
                }

                result.Add(message);
                continue;
            }

            if (message.Role == "tool")
            {
                if (string.IsNullOrWhiteSpace(message.ToolCallId))
                    continue;

                if (profile.DropOrphanToolMessages && !seenToolCallIds.Contains(message.ToolCallId))
                    continue;

                if (message.Content == null)
                    message.Content = string.Empty;

                result.Add(message);
                continue;
            }

            // Non-assistant/non-tool roles
            if (message.Content == null && !string.IsNullOrWhiteSpace(message.ReasoningContent))
            {
                message.Content = BuildReasoningFallbackContent(message.ReasoningContent);
            }

            if (message.Content != null || !string.IsNullOrWhiteSpace(message.ReasoningContent))
            {
                result.Add(message);
            }
        }

        return result;
    }

    private static bool HasAssistantPayload(OpenAiMessage message)
    {
        if (message.ToolCalls != null && message.ToolCalls.Count > 0)
            return true;

        return HasTextualAssistantContent(message);
    }

    private static bool HasTextualAssistantContent(OpenAiMessage message)
    {
        if (message.Content is null)
            return false;

        if (message.Content is string contentText)
            return !string.IsNullOrWhiteSpace(contentText);

        if (message.Content is IReadOnlyCollection<OpenAiContentPart> contentParts)
            return contentParts.Any(part => !string.IsNullOrWhiteSpace(part.Text) || part.ImageUrl != null);

        if (message.Content is IEnumerable<OpenAiContentPart> enumerableParts)
            return enumerableParts.Any(part => !string.IsNullOrWhiteSpace(part.Text) || part.ImageUrl != null);

        if (message.Content is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.String => !string.IsNullOrWhiteSpace(jsonElement.GetString()),
                JsonValueKind.Array => jsonElement.EnumerateArray().Any(item =>
                    (item.TryGetProperty("text", out var textProp) && !string.IsNullOrWhiteSpace(textProp.GetString())) ||
                    item.TryGetProperty("image_url", out _)),
                _ => !string.IsNullOrWhiteSpace(jsonElement.GetRawText())
            };
        }

        return true;
    }

    private static string BuildReasoningFallbackContent(string reasoningContent)
    {
        var trimmed = reasoningContent.Trim();
        if (trimmed.Length == 0)
            return string.Empty;

        return trimmed.Length <= 1200
            ? trimmed
            : trimmed[..1200];
    }

    private static string ExtractTextualContent(object? content)
    {
        if (content is null)
            return string.Empty;

        if (content is string text)
            return text;

        if (content is IEnumerable<OpenAiContentPart> parts)
        {
            var texts = parts
                .Select(p => p.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            return texts.Count == 0 ? string.Empty : string.Join("\n", texts);
        }

        if (content is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
                return element.GetString() ?? string.Empty;

            if (element.ValueKind == JsonValueKind.Array)
            {
                var texts = new List<string>();
                foreach (var part in element.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textProp))
                    {
                        var value = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                            texts.Add(value);
                    }
                }

                return texts.Count == 0 ? string.Empty : string.Join("\n", texts);
            }
        }

        return content.ToString() ?? string.Empty;
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

                case TextReasoningContent reasoningContent:
                    if (!string.IsNullOrWhiteSpace(reasoningContent.Text))
                    {
                        parts.Add(new OpenAiContentPart
                        {
                            Type = "text",
                            Text = reasoningContent.Text
                        });
                    }
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

public sealed class OpenAiRequestProfile
{
    public static OpenAiRequestProfile Default { get; } = new();

    public string Name { get; init; } = "openai-default";
    public bool IncludeReasoningContent { get; init; }
    public bool RequireReasoningContentForAssistantMessages { get; init; }
    public bool RequireAssistantContentOrToolCalls { get; init; } = true;
    public bool InjectAssistantContentFromReasoningWhenMissing { get; init; } = true;
    public bool DropOrphanToolMessages { get; init; } = true;
}
