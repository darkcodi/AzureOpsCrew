using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpsCrew.Domain.Chats;

namespace AzureOpsCrew.Api.Endpoints.Dtos.Channels;

/// <summary>
/// Base class for all channel events.
/// </summary>
[JsonConverter(typeof(ChannelEventJsonConverter))]
public abstract class ChannelEvent
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event fired when a new message is posted to the channel.
/// </summary>
public class MessageAddedEvent : ChannelEvent
{
    public MessageAddedEvent()
    {
        Type = ChannelEventTypes.MessageAdded;
    }

    [JsonPropertyName("message")]
    public Message Message { get; set; } = null!;
}

/// <summary>
/// Event fired when an agent starts processing/thinking.
/// </summary>
public class AgentThinkingStartEvent : ChannelEvent
{
    public AgentThinkingStartEvent()
    {
        Type = ChannelEventTypes.AgentThinkingStart;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;
}

/// <summary>
/// Event fired when an agent finishes processing/thinking.
/// </summary>
public class AgentThinkingEndEvent : ChannelEvent
{
    public AgentThinkingEndEvent()
    {
        Type = ChannelEventTypes.AgentThinkingEnd;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;
}

/// <summary>
/// Event fired with streaming text content from an agent.
/// </summary>
public class AgentTextContentEvent : ChannelEvent
{
    public AgentTextContentEvent()
    {
        Type = ChannelEventTypes.AgentTextContent;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("isDelta")]
    public bool IsDelta { get; set; } = true;
}

/// <summary>
/// Event fired when an agent starts executing a tool.
/// </summary>
public class ToolCallStartEvent : ChannelEvent
{
    public ToolCallStartEvent()
    {
        Type = ChannelEventTypes.ToolCallStart;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;
}

/// <summary>
/// Event fired when an agent finishes executing a tool.
/// </summary>
public class ToolCallEndEvent : ChannelEvent
{
    public ToolCallEndEvent()
    {
        Type = ChannelEventTypes.ToolCallEnd;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("toolCallId")]
    public string ToolCallId { get; set; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Event fired to update typing indicator status.
/// </summary>
public class TypingIndicatorEvent : ChannelEvent
{
    public TypingIndicatorEvent()
    {
        Type = ChannelEventTypes.TypingIndicator;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("isTyping")]
    public bool IsTyping { get; set; }
}

/// <summary>
/// JSON converter for channel event polymorphic deserialization.
/// </summary>
public class ChannelEventJsonConverter : JsonConverter<ChannelEvent>
{
    private const string TypeDiscriminatorPropertyName = "type";

    public override bool CanConvert(Type typeToConvert) =>
        typeof(ChannelEvent).IsAssignableFrom(typeToConvert);

    public override ChannelEvent? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var jsonElementTypeInfo = options.GetTypeInfo(typeof(JsonElement));
        JsonElement jsonElement = (JsonElement)JsonSerializer.Deserialize(ref reader, jsonElementTypeInfo)!;

        if (!jsonElement.TryGetProperty(TypeDiscriminatorPropertyName, out JsonElement discriminatorElement))
        {
            throw new JsonException($"Missing required property '{TypeDiscriminatorPropertyName}' for ChannelEvent deserialization");
        }

        string? discriminator = discriminatorElement.GetString();

        ChannelEvent? result = discriminator switch
        {
            ChannelEventTypes.MessageAdded => jsonElement.Deserialize(options.GetTypeInfo(typeof(MessageAddedEvent))) as MessageAddedEvent,
            ChannelEventTypes.AgentThinkingStart => jsonElement.Deserialize(options.GetTypeInfo(typeof(AgentThinkingStartEvent))) as AgentThinkingStartEvent,
            ChannelEventTypes.AgentThinkingEnd => jsonElement.Deserialize(options.GetTypeInfo(typeof(AgentThinkingEndEvent))) as AgentThinkingEndEvent,
            ChannelEventTypes.AgentTextContent => jsonElement.Deserialize(options.GetTypeInfo(typeof(AgentTextContentEvent))) as AgentTextContentEvent,
            ChannelEventTypes.ToolCallStart => jsonElement.Deserialize(options.GetTypeInfo(typeof(ToolCallStartEvent))) as ToolCallStartEvent,
            ChannelEventTypes.ToolCallEnd => jsonElement.Deserialize(options.GetTypeInfo(typeof(ToolCallEndEvent))) as ToolCallEndEvent,
            ChannelEventTypes.TypingIndicator => jsonElement.Deserialize(options.GetTypeInfo(typeof(TypingIndicatorEvent))) as TypingIndicatorEvent,
            _ => throw new JsonException($"Unknown ChannelEvent type discriminator: '{discriminator}'")
        };

        if (result == null)
        {
            throw new JsonException($"Failed to deserialize ChannelEvent with type discriminator: '{discriminator}'");
        }

        return result;
    }

    public override void Write(
        Utf8JsonWriter writer,
        ChannelEvent value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case MessageAddedEvent messageAdded:
                JsonSerializer.Serialize(writer, messageAdded, options.GetTypeInfo(typeof(MessageAddedEvent)));
                break;
            case AgentThinkingStartEvent thinkingStart:
                JsonSerializer.Serialize(writer, thinkingStart, options.GetTypeInfo(typeof(AgentThinkingStartEvent)));
                break;
            case AgentThinkingEndEvent thinkingEnd:
                JsonSerializer.Serialize(writer, thinkingEnd, options.GetTypeInfo(typeof(AgentThinkingEndEvent)));
                break;
            case AgentTextContentEvent textContent:
                JsonSerializer.Serialize(writer, textContent, options.GetTypeInfo(typeof(AgentTextContentEvent)));
                break;
            case ToolCallStartEvent toolCallStart:
                JsonSerializer.Serialize(writer, toolCallStart, options.GetTypeInfo(typeof(ToolCallStartEvent)));
                break;
            case ToolCallEndEvent toolCallEnd:
                JsonSerializer.Serialize(writer, toolCallEnd, options.GetTypeInfo(typeof(ToolCallEndEvent)));
                break;
            case TypingIndicatorEvent typingIndicator:
                JsonSerializer.Serialize(writer, typingIndicator, options.GetTypeInfo(typeof(TypingIndicatorEvent)));
                break;
            default:
                throw new InvalidOperationException($"Unknown channel event type: {value.GetType().Name}");
        }
    }
}
