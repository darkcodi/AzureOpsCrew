using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace AzureOpsCrew.Infrastructure.Ai.Extensions
{
    /// <summary>
    /// Reflection shim that exposes the internal Microsoft.Extensions.AI.OpenAIJsonContext members:
    ///   - Default.JsonElement
    ///   - Default.ToolJson
    ///   - Default.IDictionaryStringObject
    ///
    /// This is intentionally hacky. It can break on package updates and may fail under trimming/NativeAOT.
    /// </summary>
    public sealed class OpenAIJsonContext
    {
        private readonly JsonSerializerContext _inner;
        private readonly Type _innerType;

        private OpenAIJsonContext(JsonSerializerContext inner)
        {
            _inner = inner;
            _innerType = inner.GetType();
        }

        private static readonly Lazy<OpenAIJsonContext> s_default =
            new(CreateDefault, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>Equivalent to the internal OpenAIJsonContext.Default.</summary>
        public static OpenAIJsonContext Default => s_default.Value;

        private JsonTypeInfo<JsonElement>? _jsonElement;
        private JsonTypeInfo? _toolJson;
        private JsonTypeInfo<IDictionary<string, object?>>? _dictionaryStringObject;

        /// <summary>Equivalent to internal OpenAIJsonContext.Default.JsonElement.</summary>
        public JsonTypeInfo<JsonElement> JsonElement =>
            _jsonElement ??= GetTypeInfoOrThrow<JsonElement>("JsonElement");

        /// <summary>
        /// Equivalent to internal OpenAIJsonContext.Default.ToolJson.
        /// Note: ToolJson's underlying model type is internal, so this is exposed as non-generic JsonTypeInfo.
        /// </summary>
        public JsonTypeInfo ToolJson =>
            _toolJson ??= GetToolJsonTypeInfoOrThrow();

        /// <summary>Equivalent to internal OpenAIJsonContext.Default.IDictionaryStringObject.</summary>
        public JsonTypeInfo<IDictionary<string, object?>> IDictionaryStringObject =>
            _dictionaryStringObject ??= GetTypeInfoOrThrow<IDictionary<string, object?>>("IDictionaryStringObject");

        [RequiresUnreferencedCode("Uses reflection over internal types; trimming can remove required metadata.")]
        [RequiresDynamicCode("Uses reflection over internal types; NativeAOT may not support this reliably.")]
        private static OpenAIJsonContext CreateDefault()
        {
            // The internal OpenAIJsonContext lives in the Microsoft.Extensions.AI.OpenAI assembly.
            Assembly openAiAsm = typeof(Microsoft.Extensions.AI.OpenAIClientExtensions).Assembly;

            // Full name per source: namespace Microsoft.Extensions.AI; internal sealed partial class OpenAIJsonContext : JsonSerializerContext; :contentReference[oaicite:1]{index=1}
            Type ctxType = openAiAsm.GetType("Microsoft.Extensions.AI.OpenAIJsonContext", throwOnError: true)!;

            PropertyInfo defaultProp =
                ctxType.GetProperty("Default", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMemberException(ctxType.FullName, "Default");

            object? inner = defaultProp.GetValue(null);
            if (inner is not JsonSerializerContext jsc)
                throw new InvalidOperationException($"{ctxType.FullName}.Default was not a JsonSerializerContext.");

            return new OpenAIJsonContext(jsc);
        }

        [RequiresUnreferencedCode("Uses reflection over internal types; trimming can remove required metadata.")]
        [RequiresDynamicCode("Uses reflection over internal types; NativeAOT may not support this reliably.")]
        private JsonTypeInfo<T> GetTypeInfoOrThrow<T>(string generatedPropertyName)
        {
            // Prefer the source-generated property (e.g., JsonElement / IDictionaryStringObject)
            PropertyInfo? p = _innerType.GetProperty(
                generatedPropertyName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (p?.GetValue(_inner) is JsonTypeInfo<T> ti)
                return ti;

            // Fallback: ask the context for the type info by Type
            JsonTypeInfo? ti2 = _inner.GetTypeInfo(typeof(T));
            if (ti2 is JsonTypeInfo<T> typed)
                return typed;

            throw new MissingMemberException(
                _innerType.FullName,
                $"{generatedPropertyName} (and GetTypeInfo(typeof({typeof(T).FullName})) returned null / wrong type)");
        }

        [RequiresUnreferencedCode("Uses reflection over internal types; trimming can remove required metadata.")]
        [RequiresDynamicCode("Uses reflection over internal types; NativeAOT may not support this reliably.")]
        private JsonTypeInfo GetToolJsonTypeInfoOrThrow()
        {
            // Prefer the generated ToolJson property (JsonTypeInfo<OpenAIClientExtensions.ToolJson>)
            PropertyInfo? p = _innerType.GetProperty(
                "ToolJson",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (p?.GetValue(_inner) is JsonTypeInfo ti)
                return ti;

            // Fallback: locate the internal nested type OpenAIClientExtensions.ToolJson and ask the context for it
            Type? toolJsonModelType = typeof(Microsoft.Extensions.AI.OpenAIClientExtensions).GetNestedType(
                "ToolJson",
                BindingFlags.Public | BindingFlags.NonPublic);

            if (toolJsonModelType is null)
                throw new TypeLoadException("Could not find nested type OpenAIClientExtensions.ToolJson.");

            JsonTypeInfo? ti2 = _inner.GetTypeInfo(toolJsonModelType);
            return ti2 ?? throw new MissingMemberException(
                _innerType.FullName,
                "ToolJson (and GetTypeInfo(OpenAIClientExtensions.ToolJson) returned null)");
        }
    }
}
