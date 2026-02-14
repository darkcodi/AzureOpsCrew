using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AzureOpsCrew.Api.Endpoints.Dtos.AGUI;

public class AGUIJsonSerializerContext : JsonSerializerContext
{
    private static readonly JsonSerializerOptions s_defaultOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false,
    };

    public AGUIJsonSerializerContext(JsonSerializerOptions options) : base(options)
    {
    }

    public static AGUIJsonSerializerContext Default { get; } = new AGUIJsonSerializerContext(new JsonSerializerOptions(s_defaultOptions));

    private JsonTypeInfo<BaseEvent>? _BaseEvent;

    /// <summary>
    /// Defines the source generated JSON serialization contract metadata for a given type.
    /// </summary>
    public JsonTypeInfo<BaseEvent> BaseEvent
    {
        get => _BaseEvent ??= (JsonTypeInfo<BaseEvent>)Options.GetTypeInfo(typeof(BaseEvent));
    }

    private JsonTypeInfo<JsonElement>? _JsonElement;

    /// <summary>
    /// Defines the source generated JSON serialization contract metadata for a given type.
    /// </summary>
    public JsonTypeInfo<JsonElement> JsonElement
    {
        get => _JsonElement ??= (JsonTypeInfo<JsonElement>)Options.GetTypeInfo(typeof(JsonElement));
    }

    public override JsonTypeInfo? GetTypeInfo(Type type)
    {
        Options.TryGetTypeInfo(type, out JsonTypeInfo? typeInfo);
        return typeInfo;
    }

    protected override JsonSerializerOptions? GeneratedSerializerOptions { get; }
}
