using System.ClientModel.Primitives;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using OpenAI;

namespace AzureOpsCrew.Infrastructure.Ai.Extensions;

/// <summary>Provides extension methods for working with <see cref="OpenAIClient"/>s.</summary>
public static class OpenAIClientExtensions
{
    /// <summary>Key into AdditionalProperties used to store a strict option.</summary>
    private const string StrictKey = "strict";

    /// <summary>Gets a <see cref="ChatRole"/> for "developer".</summary>
    internal static ChatRole ChatRoleDeveloper { get; } = new ChatRole("developer");

    /// <summary>
    /// Gets the JSON schema transformer cache conforming to OpenAI <b>strict</b> / structured output restrictions per
    /// https://platform.openai.com/docs/guides/structured-outputs?api-mode=responses#supported-schemas.
    /// </summary>
    internal static AIJsonSchemaTransformCache StrictSchemaTransformCache { get; } = new(new()
    {
        DisallowAdditionalProperties = true,
        ConvertBooleanSchemas = true,
        MoveDefaultKeywordToDescription = true,
        RequireAllProperties = true,
        TransformSchemaNode = (ctx, node) =>
        {
            // Move content from common but unsupported properties to description. In particular, we focus on properties that
            // the AIJsonUtilities schema generator might produce and/or that are explicitly mentioned in the OpenAI documentation.

            if (node is JsonObject schemaObj)
            {
                StringBuilder? additionalDescription = null;

                ReadOnlySpan<string> unsupportedProperties =
                [
                    // Produced by AIJsonUtilities but not in allow list at https://platform.openai.com/docs/guides/structured-outputs#supported-properties:
                    "contentEncoding", "contentMediaType", "not",

                    // Explicitly mentioned at https://platform.openai.com/docs/guides/structured-outputs?api-mode=responses#key-ordering as being unsupported with some models:
                    "minLength", "maxLength", "pattern", "format",
                    "minimum", "maximum", "multipleOf",
                    "patternProperties",
                    "minItems", "maxItems",

                    // Explicitly mentioned at https://learn.microsoft.com/azure/ai-services/openai/how-to/structured-outputs?pivots=programming-language-csharp&tabs=python-secure%2Cdotnet-entra-id#unsupported-type-specific-keywords
                    // as being unsupported with Azure OpenAI:
                    "unevaluatedProperties", "propertyNames", "minProperties", "maxProperties",
                    "unevaluatedItems", "contains", "minContains", "maxContains", "uniqueItems",
                ];

                foreach (string propName in unsupportedProperties)
                {
                    if (schemaObj[propName] is { } propNode)
                    {
                        _ = schemaObj.Remove(propName);
                        AppendLine(ref additionalDescription, propName, propNode);
                    }
                }

                if (additionalDescription is not null)
                {
                    schemaObj["description"] = schemaObj["description"] is { } descriptionNode && descriptionNode.GetValueKind() == JsonValueKind.String ?
                        $"{descriptionNode.GetValue<string>()}{Environment.NewLine}{additionalDescription}" :
                        additionalDescription.ToString();
                }

                return node;

                static void AppendLine(ref StringBuilder? sb, string propName, JsonNode propNode)
                {
                    sb ??= new();

                    if (sb.Length > 0)
                    {
                        _ = sb.AppendLine();
                    }

                    _ = sb.Append(propName).Append(": ").Append(propNode);
                }
            }

            return node;
        },
    });

    /// <summary>Extracts from an <see cref="AIFunctionDeclaration"/> the parameters and strictness setting for use with OpenAI's APIs.</summary>
    internal static BinaryData ToOpenAIFunctionParameters(AIFunctionDeclaration aiFunction, bool? strict)
    {
        // Perform any desirable transformations on the function's JSON schema, if it'll be used in a strict setting.
        JsonElement jsonSchema = strict is true ?
            StrictSchemaTransformCache.GetOrCreateTransformedSchema(aiFunction) :
            aiFunction.JsonSchema;

        // Roundtrip the schema through the ToolJson model type to force missing properties
        // into existence, then return the serialized UTF8 bytes as BinaryData.
        var tool = JsonSerializer.Deserialize(jsonSchema, OpenAIJsonContext.Default.ToolJson)!;
        var functionParameters = BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(tool, OpenAIJsonContext.Default.ToolJson));

        return functionParameters;
    }

    /// <summary>Creates a new instance of <see cref="FunctionCallContent"/> parsing arguments using a specified encoding and parser.</summary>
    /// <param name="json">The input arguments to be parsed.</param>
    /// <param name="callId">The function call ID.</param>
    /// <param name="name">The function name.</param>
    /// <returns>A new instance of <see cref="FunctionCallContent"/> containing the parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    internal static FunctionCallContent ParseCallContent(string json, string callId, string name) =>
        FunctionCallContent.CreateFromParsedArguments(json, callId, name,
            static json => JsonSerializer.Deserialize(json, OpenAIJsonContext.Default.IDictionaryStringObject)!);

    /// <summary>Creates a new instance of <see cref="FunctionCallContent"/> parsing arguments using a specified encoding and parser.</summary>
    /// <param name="utf8json">The input arguments to be parsed.</param>
    /// <param name="callId">The function call ID.</param>
    /// <param name="name">The function name.</param>
    /// <returns>A new instance of <see cref="FunctionCallContent"/> containing the parse result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="callId"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    internal static FunctionCallContent ParseCallContent(BinaryData utf8json, string callId, string name) =>
        FunctionCallContent.CreateFromParsedArguments(utf8json, callId, name,
            static utf8json => JsonSerializer.Deserialize(utf8json, OpenAIJsonContext.Default.IDictionaryStringObject)!);

    internal static bool? HasStrict(IReadOnlyDictionary<string, object?>? additionalProperties) =>
        additionalProperties?.TryGetValue(StrictKey, out object? strictObj) is true &&
        strictObj is bool strictValue ?
            strictValue : null;

    /// <summary>Gets a media type for an image based on the file extension in the provided URI.</summary>
    internal static string ImageUriToMediaType(Uri uri)
    {
        return MediaTypeMap.GetMediaType(uri.AbsoluteUri) ?? "image/*";
    }

    /// <summary>Sets $.model in <paramref name="patch"/> to <paramref name="modelId"/> if not already set.</summary>
    [Experimental("SCME0001")]
    internal static void PatchModelIfNotSet(ref JsonPatch patch, string? modelId)
    {
        if (modelId is not null)
        {
            _ = patch.TryGetValue("$.model"u8, out string? existingModel);
            if (existingModel is null)
            {
                patch.Set("$.model"u8, modelId);
            }
        }
    }
}
