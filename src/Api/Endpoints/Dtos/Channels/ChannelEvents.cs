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
/// Event fired when an agent completes a tool call (with result or error).
/// </summary>
public class ToolCallCompletedEvent : ChannelEvent
{
    public ToolCallCompletedEvent()
    {
        Type = ChannelEventTypes.ToolCallCompleted;
    }

    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("callId")]
    public string CallId { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public object? Args { get; set; }

    [JsonPropertyName("result")]
    public object? Result { get; set; }

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    [JsonPropertyName("timestamp")]
    public new DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Event fired when an agent produces reasoning content during processing.
/// </summary>
public class ReasoningContentEvent : ChannelEvent
{
    public ReasoningContentEvent()
    {
        Type = ChannelEventTypes.ReasoningContent;
    }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public new DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Event fired when an agent's status changes (e.g. Running, Idle, Error).
/// </summary>
public class AgentStatusEvent : ChannelEvent
{
    public AgentStatusEvent()
    {
        Type = ChannelEventTypes.AgentStatus;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    [JsonPropertyName("timestamp")]
    public new DateTimeOffset Timestamp { get; set; }
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
            ChannelEventTypes.ToolCallCompleted => jsonElement.Deserialize(options.GetTypeInfo(typeof(ToolCallCompletedEvent))) as ToolCallCompletedEvent,
            ChannelEventTypes.ReasoningContent => jsonElement.Deserialize(options.GetTypeInfo(typeof(ReasoningContentEvent))) as ReasoningContentEvent,
            ChannelEventTypes.AgentStatus => jsonElement.Deserialize(options.GetTypeInfo(typeof(AgentStatusEvent))) as AgentStatusEvent,
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
            case ToolCallCompletedEvent toolCallCompleted:
                JsonSerializer.Serialize(writer, toolCallCompleted, options.GetTypeInfo(typeof(ToolCallCompletedEvent)));
                break;
            case ReasoningContentEvent reasoningContent:
                JsonSerializer.Serialize(writer, reasoningContent, options.GetTypeInfo(typeof(ReasoningContentEvent)));
                break;
            case AgentStatusEvent agentStatus:
                JsonSerializer.Serialize(writer, agentStatus, options.GetTypeInfo(typeof(AgentStatusEvent)));
                break;
            default:
                throw new InvalidOperationException($"Unknown channel event type: {value.GetType().Name}");
        }
    }
}
