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
/// Event fired to update user presence status.
/// </summary>
public class UserPresenceEvent : ChannelEvent
{
    public UserPresenceEvent()
    {
        Type = ChannelEventTypes.UserPresence;
    }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("isOnline")]
    public bool IsOnline { get; set; }
}

/// <summary>
/// Event fired to update agent status.
/// </summary>
public class AgentStatusEvent : ChannelEvent
{
    public AgentStatusEvent()
    {
        Type = ChannelEventTypes.AgentStatus;
    }

    [JsonPropertyName("agentId")]
    public Guid AgentId { get; set; }

    [JsonPropertyName("agentName")]
    public string AgentName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Event type constants for channel events.
/// </summary>
public static class ChannelEventTypes
{
    public const string MessageAdded = "MESSAGE_ADDED";
    public const string AgentThinkingStart = "AGENT_THINKING_START";
    public const string AgentThinkingEnd = "AGENT_THINKING_END";
    public const string AgentTextContent = "AGENT_TEXT_CONTENT";
    public const string ToolCallStart = "TOOL_CALL_START";
    public const string ToolCallEnd = "TOOL_CALL_END";
    public const string TypingIndicator = "TYPING_INDICATOR";
    public const string UserPresence = "USER_PRESENCE";
    public const string AgentStatus = "AGENT_STATUS";
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
            ChannelEventTypes.AgentThinkingStart => jsonElement.Deserialize<AgentThinkingStartEvent>(options),
            ChannelEventTypes.AgentThinkingEnd => jsonElement.Deserialize<AgentThinkingEndEvent>(options),
            ChannelEventTypes.AgentTextContent => jsonElement.Deserialize<AgentTextContentEvent>(options),
            ChannelEventTypes.ToolCallStart => jsonElement.Deserialize<ToolCallStartEvent>(options),
            ChannelEventTypes.ToolCallEnd => jsonElement.Deserialize<ToolCallEndEvent>(options),
            ChannelEventTypes.TypingIndicator => jsonElement.Deserialize<TypingIndicatorEvent>(options),
            ChannelEventTypes.UserPresence => jsonElement.Deserialize<UserPresenceEvent>(options),
            ChannelEventTypes.AgentStatus => jsonElement.Deserialize<AgentStatusEvent>(options),
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
