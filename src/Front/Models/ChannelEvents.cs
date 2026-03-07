using System.Text.Json;
using System.Text.Json.Serialization;

namespace Front.Models;

/// <summary>
/// Base class for all channel events received from SignalR.
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
    public ChatMessageDto Message { get; set; } = null!;
}

/// <summary>
/// Event fired when an agent completes a tool call.
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
    public JsonElement? Args { get; set; }

    [JsonPropertyName("result")]
    public JsonElement? Result { get; set; }

    [JsonPropertyName("isError")]
    public bool IsError { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }
}

/// <summary>
/// Event type constants for channel events.
/// </summary>
/// <summary>
/// Event fired when an agent produces reasoning content.
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
    public DateTimeOffset Timestamp { get; set; }
}

public static class ChannelEventTypes
{
    public const string MessageAdded = "MESSAGE_ADDED";
    public const string ToolCallCompleted = "TOOL_CALL_COMPLETED";
    public const string ReasoningContent = "REASONING_CONTENT";
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
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(ref reader);

        if (!jsonElement.TryGetProperty(TypeDiscriminatorPropertyName, out JsonElement discriminatorElement))
        {
            return null;
        }

        string? discriminator = discriminatorElement.GetString();

        return discriminator switch
        {
            ChannelEventTypes.MessageAdded => jsonElement.Deserialize<MessageAddedEvent>(options),
            ChannelEventTypes.ToolCallCompleted => jsonElement.Deserialize<ToolCallCompletedEvent>(options),
            ChannelEventTypes.ReasoningContent => jsonElement.Deserialize<ReasoningContentEvent>(options),
            _ => null
        };
    }

    public override void Write(
        Utf8JsonWriter writer,
        ChannelEvent value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
