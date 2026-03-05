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
            default:
                throw new InvalidOperationException($"Unknown channel event type: {value.GetType().Name}");
        }
    }
}
