using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

[JsonConverter(typeof(AGUIMessageJsonConverter))]
public class AGUIMessage
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

public class AGUIAssistantMessage : AGUIMessage
{
    public AGUIAssistantMessage()
    {
        Role = AGUIRoles.Assistant;
    }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("toolCalls")]
    public AGUIToolCall[]? ToolCalls { get; set; }
}

public class AGUIDeveloperMessage : AGUIMessage
{
    public AGUIDeveloperMessage()
    {
        Role = AGUIRoles.Developer;
    }
}

public class AGUISystemMessage : AGUIMessage
{
    public AGUISystemMessage()
    {
        Role = AGUIRoles.System;
    }
}

public class AGUIToolMessage : AGUIMessage
{
    public AGUIToolMessage()
    {
        Role = AGUIRoles.Tool;
    }

    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class AGUIUserMessage : AGUIMessage
{
    public AGUIUserMessage()
    {
        Role = AGUIRoles.User;
    }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class AGUIMessageJsonConverter : JsonConverter<AGUIMessage>
{
    private const string RoleDiscriminatorPropertyName = "role";

    public override bool CanConvert(Type typeToConvert) =>
        typeof(AGUIMessage).IsAssignableFrom(typeToConvert);

    public override AGUIMessage Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonElementTypeInfo = options.GetTypeInfo(typeof(JsonElement));
        JsonElement jsonElement = (JsonElement)JsonSerializer.Deserialize(ref reader, jsonElementTypeInfo)!;

        // Try to get the discriminator property
        if (!jsonElement.TryGetProperty(RoleDiscriminatorPropertyName, out JsonElement discriminatorElement))
        {
            throw new JsonException($"Missing required property '{RoleDiscriminatorPropertyName}' for AGUIMessage deserialization");
        }

        string? discriminator = discriminatorElement.GetString();

        // Map discriminator to concrete type and deserialize using type info from options
        AGUIMessage? result = discriminator switch
        {
            AGUIRoles.Developer => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUIDeveloperMessage))) as AGUIDeveloperMessage,
            AGUIRoles.System => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUISystemMessage))) as AGUISystemMessage,
            AGUIRoles.User => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUIUserMessage))) as AGUIUserMessage,
            AGUIRoles.Assistant => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUIAssistantMessage))) as AGUIAssistantMessage,
            AGUIRoles.Tool => jsonElement.Deserialize(options.GetTypeInfo(typeof(AGUIToolMessage))) as AGUIToolMessage,
            _ => throw new JsonException($"Unknown AGUIMessage role discriminator: '{discriminator}'")
        };

        if (result == null)
        {
            throw new JsonException($"Failed to deserialize AGUIMessage with role discriminator: '{discriminator}'");
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        AGUIMessage value,
        JsonSerializerOptions options)
    {
        // Serialize the concrete type directly using type info from options
        switch (value)
        {
            case AGUIDeveloperMessage developer:
                JsonSerializer.Serialize(writer, developer, options.GetTypeInfo(typeof(AGUIDeveloperMessage)));
                break;
            case AGUISystemMessage system:
                JsonSerializer.Serialize(writer, system, options.GetTypeInfo(typeof(AGUISystemMessage)));
                break;
            case AGUIUserMessage user:
                JsonSerializer.Serialize(writer, user, options.GetTypeInfo(typeof(AGUIUserMessage)));
                break;
            case AGUIAssistantMessage assistant:
                JsonSerializer.Serialize(writer, assistant, options.GetTypeInfo(typeof(AGUIAssistantMessage)));
                break;
            case AGUIToolMessage tool:
                JsonSerializer.Serialize(writer, tool, options.GetTypeInfo(typeof(AGUIToolMessage)));
                break;
            default:
                throw new JsonException($"Unknown AGUIMessage type: {value.GetType().Name}");
        }
    }
}

public static class AGUIChatMessageExtensions
{
    private static readonly ChatRole s_developerChatRole = new("developer");

    public static IEnumerable<ChatMessage> AsChatMessages(
        this IEnumerable<AGUIMessage> aguiMessages,
        JsonSerializerOptions jsonSerializerOptions)
    {
        foreach (var message in aguiMessages)
        {
            var role = MapChatRole(message.Role);

            switch (message)
            {
                case AGUIToolMessage toolMessage:
                {
                    object? result;
                    if (string.IsNullOrEmpty(toolMessage.Content))
                    {
                        result = toolMessage.Content;
                    }
                    else
                    {
                        // Try to deserialize as JSON, but fall back to string if it fails
                        try
                        {
                            result = JsonSerializer.Deserialize(toolMessage.Content, AGUIJsonSerializerContext.Default.JsonElement);
                        }
                        catch (JsonException)
                        {
                            result = toolMessage.Content;
                        }
                    }

                    yield return new ChatMessage(
                        role,
                        [
                            new FunctionResultContent(
                                    toolMessage.ToolCallId,
                                    result)
                        ]);
                    break;
                }

                case AGUIAssistantMessage assistantMessage when assistantMessage.ToolCalls is { Length: > 0 }:
                {
                    var contents = new List<AIContent>();

                    if (!string.IsNullOrEmpty(assistantMessage.Content))
                    {
                        contents.Add(new TextContent(assistantMessage.Content));
                    }

                    // Add tool calls
                    foreach (var toolCall in assistantMessage.ToolCalls)
                    {
                        Dictionary<string, object?>? arguments = null;
                        if (!string.IsNullOrEmpty(toolCall.Function.Arguments))
                        {
                            arguments = (Dictionary<string, object?>?)JsonSerializer.Deserialize(
                                toolCall.Function.Arguments,
                                jsonSerializerOptions.GetTypeInfo(typeof(Dictionary<string, object?>)));
                        }

                        contents.Add(new FunctionCallContent(
                            toolCall.Id,
                            toolCall.Function.Name,
                            arguments));
                    }

                    yield return new ChatMessage(role, contents)
                    {
                        MessageId = message.Id
                    };
                    break;
                }

                default:
                {
                    string content = message switch
                    {
                        AGUIDeveloperMessage dev => dev.Content,
                        AGUISystemMessage sys => sys.Content,
                        AGUIUserMessage user => user.Content,
                        AGUIAssistantMessage asst => asst.Content,
                        _ => string.Empty
                    };

                    yield return new ChatMessage(role, content)
                    {
                        MessageId = message.Id
                    };
                    break;
                }
            }
        }
    }

    public static IEnumerable<AGUIMessage> AsAGUIMessages(
        this IEnumerable<ChatMessage> chatMessages,
        JsonSerializerOptions jsonSerializerOptions)
    {
        foreach (var message in chatMessages)
        {
            message.MessageId ??= Guid.NewGuid().ToString("N");
            if (message.Role == ChatRole.Tool)
            {
                foreach (var toolMessage in MapToolMessages(jsonSerializerOptions, message))
                {
                    yield return toolMessage;
                }
            }
            else if (message.Role == ChatRole.Assistant)
            {
                var assistantMessage = MapAssistantMessage(jsonSerializerOptions, message);
                if (assistantMessage != null)
                {
                    yield return assistantMessage;
                }
            }
            else
            {
                yield return message.Role.Value switch
                {
                    AGUIRoles.Developer => new AGUIDeveloperMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                    AGUIRoles.System => new AGUISystemMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                    AGUIRoles.User => new AGUIUserMessage { Id = message.MessageId, Content = message.Text ?? string.Empty },
                    _ => throw new InvalidOperationException($"Unknown role: {message.Role.Value}")
                };
            }
        }
    }

    private static AGUIAssistantMessage? MapAssistantMessage(JsonSerializerOptions jsonSerializerOptions, ChatMessage message)
    {
        List<AGUIToolCall>? toolCalls = null;
        string? textContent = null;

        foreach (var content in message.Contents)
        {
            if (content is FunctionCallContent functionCall)
            {
                var argumentsJson = functionCall.Arguments is null ?
                    "{}" :
                    JsonSerializer.Serialize(functionCall.Arguments, jsonSerializerOptions.GetTypeInfo(typeof(IDictionary<string, object?>)));
                toolCalls ??= [];
                toolCalls.Add(new AGUIToolCall
                {
                    Id = functionCall.CallId,
                    Type = "function",
                    Function = new AGUIFunctionCall
                    {
                        Name = functionCall.Name,
                        Arguments = argumentsJson
                    }
                });
            }
            else if (content is TextContent textContentItem)
            {
                textContent = textContentItem.Text;
            }
        }

        // Create message with tool calls and/or text content
        if (toolCalls?.Count > 0 || !string.IsNullOrEmpty(textContent))
        {
            return new AGUIAssistantMessage
            {
                Id = message.MessageId,
                Content = textContent ?? string.Empty,
                ToolCalls = toolCalls?.Count > 0 ? toolCalls.ToArray() : null
            };
        }

        return null;
    }

    private static IEnumerable<AGUIToolMessage> MapToolMessages(JsonSerializerOptions jsonSerializerOptions, ChatMessage message)
    {
        foreach (var content in message.Contents)
        {
            if (content is FunctionResultContent functionResult)
            {
                yield return new AGUIToolMessage
                {
                    Id = functionResult.CallId,
                    ToolCallId = functionResult.CallId,
                    Content = functionResult.Result is null ?
                        string.Empty :
                        JsonSerializer.Serialize(functionResult.Result, jsonSerializerOptions.GetTypeInfo(functionResult.Result.GetType()))
                };
            }
        }
    }

    public static ChatRole MapChatRole(string role) =>
        string.Equals(role, AGUIRoles.System, StringComparison.OrdinalIgnoreCase) ? ChatRole.System :
        string.Equals(role, AGUIRoles.User, StringComparison.OrdinalIgnoreCase) ? ChatRole.User :
        string.Equals(role, AGUIRoles.Assistant, StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant :
        string.Equals(role, AGUIRoles.Developer, StringComparison.OrdinalIgnoreCase) ? s_developerChatRole :
        string.Equals(role, AGUIRoles.Tool, StringComparison.OrdinalIgnoreCase) ? ChatRole.Tool :
        throw new InvalidOperationException($"Unknown chat role: {role}");
}
