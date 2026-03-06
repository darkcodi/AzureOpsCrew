using System.Text.Json;
using System.Text.RegularExpressions;
using Serilog;

namespace AzureOpsCrew.Api.Mcp;

/// <summary>
/// MCP argument normalization, validation, and auto-repair layer.
/// Addresses defect: MCP tool calls fail due to argument format mismatches (params vs parameters, wrong root shape).
/// </summary>
public static class McpArgumentNormalizer
{
    /// <summary>
    /// Patterns in MCP tool result text that indicate the call failed (even without the word "error").
    /// Used by McpToolProvider to detect non-obvious failures.
    /// NOTE: "run again with the" and "here are the available command" are NOT failures —
    /// they are the MCP server returning help/documentation which the LLM should see.
    /// </summary>
    private static readonly string[] McpFailurePatterns =
    [
        "parameters are required",
        "required when not learning",
        "missing required",
        "invalid arguments",
        "unknown command",
        "command not found",
        "is not a valid",
        "not recognized as",
        "no such command"
    ];

    /// <summary>
    /// Patterns that indicate the MCP response is a help/documentation response
    /// (listing available commands). These should NOT be treated as failures — 
    /// the LLM needs to see this data to learn the correct tool format.
    /// </summary>
    private static readonly string[] McpHelpResponsePatterns =
    [
        "here are the available command",
        "available commands and their parameters",
        "run again with the \"command\"",
        "identify the command you want to execute"
    ];

    /// <summary>
    /// Checks if MCP tool result text indicates a failed call (even without explicit "error" keyword).
    /// Returns false for help/documentation responses — those should be passed to the LLM as valid data.
    /// </summary>
    public static bool IsLikelyMcpFailure(string resultText)
    {
        if (string.IsNullOrWhiteSpace(resultText)) return false;
        var lower = resultText.ToLowerInvariant();

        // If the response is a help/documentation response listing available commands,
        // it is NOT a failure — return it to the LLM so it can learn the correct format.
        if (McpHelpResponsePatterns.Any(pattern => lower.Contains(pattern)))
        {
            Log.Information("[McpNormalizer] Response is a help/documentation response (listing available commands), NOT a failure");
            return false;
        }

        return McpFailurePatterns.Any(pattern => lower.Contains(pattern));
    }

    /// <summary>
    /// Checks if MCP tool result text is a help/documentation response listing available commands.
    /// These responses are NOT errors — they should be passed to the LLM as useful data.
    /// </summary>
    public static bool IsLikelyHelpResponse(string resultText)
    {
        if (string.IsNullOrWhiteSpace(resultText)) return false;
        var lower = resultText.ToLowerInvariant();
        return McpHelpResponsePatterns.Any(pattern => lower.Contains(pattern));
    }

    /// <summary>
    /// Normalizes and validates arguments against the MCP tool's inputSchema.
    /// Returns normalized arguments ready for MCP invocation, or throws if validation fails after repair attempts.
    /// </summary>
    public static JsonElement NormalizeAndValidate(
        string toolName,
        JsonElement inputSchema,
        JsonElement arguments)
    {
        // Step 0: Fix stringified JSON values — Claude sometimes sends nested objects as JSON strings
        arguments = FixStringifiedJsonValues(toolName, arguments);

        // Step 1: Check if inputSchema expects a specific root property (e.g., "parameters")
        if (inputSchema.ValueKind == JsonValueKind.Object &&
            inputSchema.TryGetProperty("properties", out var properties))
        {
            // Check for common root wrappers in MCP schemas
            if (properties.TryGetProperty("parameters", out var parametersSchema))
            {
                // Schema expects { "parameters": { ... } }
                // If arguments is already wrapped, check if the inner 'parameters' is valid
                if (arguments.ValueKind == JsonValueKind.Object &&
                    arguments.TryGetProperty("parameters", out var existingParams))
                {
                    // If 'parameters' is already a proper object, pass through
                    if (existingParams.ValueKind == JsonValueKind.Object)
                    {
                        Log.Debug("[McpNormalizer] Tool {Tool}: arguments already have 'parameters' root with object value, validation OK", toolName);
                        return arguments;
                    }

                    // If 'parameters' is a string (Claude sometimes stringifies nested JSON), parse it
                    if (existingParams.ValueKind == JsonValueKind.String)
                    {
                        var parsedParams = TryParseJsonString(existingParams.GetString());
                        if (parsedParams.HasValue)
                        {
                            Log.Information("[McpNormalizer] Tool {Tool}: parsed stringified 'parameters' JSON string into object", toolName);
                            // Merge: take 'command' from siblings if present, merge with parsed params
                            return RestructureWithParsedParameters(toolName, arguments, parsedParams.Value);
                        }
                    }
                    
                    // Parameters exists but is not object and not parseable string — pass through
                    Log.Debug("[McpNormalizer] Tool {Tool}: arguments have 'parameters' root (non-object), passing through", toolName);
                    return arguments;
                }

                // If arguments has "params" instead of "parameters", remap
                if (arguments.ValueKind == JsonValueKind.Object &&
                    arguments.TryGetProperty("params", out var paramsValue))
                {
                    Log.Information("[McpNormalizer] Tool {Tool}: remapping 'params' → 'parameters'", toolName);
                    return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                    {
                        ["parameters"] = JsonSerializer.Deserialize<object>(paramsValue)
                    });
                }

                // If arguments is a flat object but schema expects { parameters: {...} }, wrap it
                if (arguments.ValueKind == JsonValueKind.Object)
                {
                    // Check if the flat object matches the nested schema
                    var flatKeys = arguments.EnumerateObject().Select(p => p.Name).ToHashSet();
                    var expectedKeys = parametersSchema.ValueKind == JsonValueKind.Object && parametersSchema.TryGetProperty("properties", out var nestedProps)
                        ? nestedProps.EnumerateObject().Select(p => p.Name).ToHashSet()
                        : new HashSet<string>();

                    // If there's significant overlap, wrap it
                    if (expectedKeys.Count > 0 && flatKeys.Intersect(expectedKeys).Any())
                    {
                        Log.Information("[McpNormalizer] Tool {Tool}: wrapping flat args into 'parameters' root", toolName);
                        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                        {
                            ["parameters"] = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments)
                        });
                    }
                }
            }

            // Check for "command" wrapper (some MCP servers use { "command": "...", "args": {...} })
            if (properties.TryGetProperty("command", out _) && properties.TryGetProperty("args", out _))
            {
                // If arguments already has command+args, return as-is
                if (arguments.ValueKind == JsonValueKind.Object &&
                    arguments.TryGetProperty("command", out _) &&
                    arguments.TryGetProperty("args", out _))
                {
                    Log.Debug("[McpNormalizer] Tool {Tool}: arguments already have 'command+args' structure, validation OK", toolName);
                    return arguments;
                }

                // Attempt to infer command from tool name and wrap args
                if (arguments.ValueKind == JsonValueKind.Object &&
                    !arguments.TryGetProperty("command", out _))
                {
                    var inferredCommand = InferCommandFromToolName(toolName);
                    if (!string.IsNullOrEmpty(inferredCommand))
                    {
                        Log.Information("[McpNormalizer] Tool {Tool}: wrapping args with inferred command '{Command}'", toolName, inferredCommand);
                        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                        {
                            ["command"] = inferredCommand,
                            ["args"] = JsonSerializer.Deserialize<Dictionary<string, object?>>(arguments)
                        });
                    }
                }
            }
        }

        // Step 2: Validate required properties against schema
        if (inputSchema.ValueKind == JsonValueKind.Object &&
            inputSchema.TryGetProperty("required", out var required) &&
            required.ValueKind == JsonValueKind.Array)
        {
            var missingFields = new List<string>();
            var argsObject = arguments.ValueKind == JsonValueKind.Object ? arguments : default;

            foreach (var requiredField in required.EnumerateArray())
            {
                var fieldName = requiredField.GetString();
                if (fieldName is not null && !argsObject.TryGetProperty(fieldName, out _))
                {
                    missingFields.Add(fieldName);
                }
            }

            if (missingFields.Count > 0)
            {
                var hint = $"Tool '{toolName}' requires these fields: {string.Join(", ", missingFields)}. " +
                           "Check the tool schema or provide default values if possible.";
                Log.Warning("[McpNormalizer] Tool {Tool}: missing required fields: {Fields}. {Hint}",
                    toolName, string.Join(", ", missingFields), hint);
                // Don't throw yet — let MCP server return the error, we'll catch it in retry logic
            }
        }

        // No normalization needed or possible
        Log.Debug("[McpNormalizer] Tool {Tool}: arguments passed through unchanged", toolName);
        return arguments;
    }

    /// <summary>
    /// Parses MCP error response and returns a repair strategy if applicable.
    /// </summary>
    public static RepairStrategy? ParseErrorAndSuggestRepair(string toolName, JsonElement errorResponse)
    {
        // Extract error message from MCP response structure
        string? errorMessage = null;
        if (errorResponse.TryGetProperty("error", out var errorObj))
        {
            if (errorObj.TryGetProperty("message", out var msg))
                errorMessage = msg.GetString();
            else if (errorObj.ValueKind == JsonValueKind.String)
                errorMessage = errorObj.GetString();
        }
        else if (errorResponse.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
        {
            // Some MCP servers return error in content[].text
            foreach (var item in contentArray.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text))
                {
                    errorMessage ??= text.GetString();
                }
            }
        }

        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            Log.Debug("[McpNormalizer] Tool {Tool}: no parseable error message in response", toolName);
            return null;
        }

        errorMessage = errorMessage.ToLowerInvariant();

        // Known error patterns and repair strategies
        if (errorMessage.Contains("missing required") && errorMessage.Contains("parameters"))
        {
            Log.Information("[McpNormalizer] Tool {Tool}: detected 'missing required parameters' error", toolName);
            return new RepairStrategy
            {
                Type = RepairType.WrapInParametersRoot,
                Reason = "MCP server expects arguments wrapped in 'parameters' root object"
            };
        }

        if (errorMessage.Contains("wrap") && errorMessage.Contains("parameters"))
        {
            Log.Information("[McpNormalizer] Tool {Tool}: detected 'wrap into parameters' error", toolName);
            return new RepairStrategy
            {
                Type = RepairType.WrapInParametersRoot,
                Reason = "MCP server explicitly requested wrapping in 'parameters'"
            };
        }

        if (errorMessage.Contains("command") && errorMessage.Contains("required"))
        {
            Log.Information("[McpNormalizer] Tool {Tool}: detected 'command required' error", toolName);
            return new RepairStrategy
            {
                Type = RepairType.InferCommandWrapper,
                Reason = "MCP server expects { command, args } structure"
            };
        }

        // Extract missing field names from error message
        var missingFields = ExtractMissingFieldsFromError(errorMessage);
        if (missingFields.Count > 0)
        {
            Log.Information("[McpNormalizer] Tool {Tool}: detected missing fields: {Fields}", toolName, string.Join(", ", missingFields));
            return new RepairStrategy
            {
                Type = RepairType.ProvideMissingFields,
                Reason = $"MCP tool requires these fields: {string.Join(", ", missingFields)}",
                MissingFields = missingFields
            };
        }

        Log.Debug("[McpNormalizer] Tool {Tool}: no known repair strategy for error: {Error}", toolName, errorMessage);
        return null;
    }

    /// <summary>
    /// Applies a repair strategy to arguments and returns repaired version.
    /// </summary>
    public static JsonElement ApplyRepair(RepairStrategy strategy, JsonElement originalArgs, string? toolName = null)
    {
        switch (strategy.Type)
        {
            case RepairType.WrapInParametersRoot:
                Log.Information("[McpNormalizer] Applying WrapInParametersRoot repair");
                return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                {
                    ["parameters"] = JsonSerializer.Deserialize<Dictionary<string, object?>>(originalArgs)
                });

            case RepairType.InferCommandWrapper:
                if (!string.IsNullOrEmpty(toolName))
                {
                    // Prevent double-wrapping: if args already have a "command" key, don't wrap again
                    if (originalArgs.ValueKind == JsonValueKind.Object &&
                        originalArgs.TryGetProperty("command", out _))
                    {
                        Log.Warning("[McpNormalizer] Args already contain 'command' key, skipping InferCommandWrapper to avoid double-wrap");
                        return originalArgs;
                    }
                    
                    var inferredCommand = InferCommandFromToolName(toolName);
                    if (!string.IsNullOrEmpty(inferredCommand))
                    {
                        Log.Information("[McpNormalizer] Applying InferCommandWrapper repair: wrapping with command '{Command}'", inferredCommand);
                        return JsonSerializer.SerializeToElement(new Dictionary<string, object?>
                        {
                            ["command"] = inferredCommand,
                            ["parameters"] = JsonSerializer.Deserialize<Dictionary<string, object?>>(originalArgs)
                        });
                    }
                }
                Log.Warning("[McpNormalizer] Cannot apply InferCommandWrapper repair without tool name context or unable to infer command");
                return originalArgs;

            case RepairType.ProvideMissingFields:
                // Cannot auto-provide missing fields without knowing their types/defaults
                // But we can at least log what's missing for debugging
                Log.Warning("[McpNormalizer] Cannot auto-provide missing fields: {Fields}. " +
                           "The agent should provide these values or the user needs to supply them.",
                    string.Join(", ", strategy.MissingFields ?? []));
                return originalArgs;

            default:
                return originalArgs;
        }
    }

    /// <summary>
    /// Attempts to apply multiple repair strategies in sequence until one works.
    /// Returns the repaired arguments or the original if no repair succeeded.
    /// </summary>
    public static JsonElement ApplyRepairsSequentially(JsonElement arguments, string toolName, JsonElement inputSchema)
    {
        var strategies = new[]
        {
            RepairType.WrapInParametersRoot,
            RepairType.InferCommandWrapper
        };

        foreach (var strategyType in strategies)
        {
            var strategy = new RepairStrategy { Type = strategyType };
            var repaired = ApplyRepair(strategy, arguments, toolName);

            // Validate the repaired arguments against schema
            if (!repaired.Equals(arguments))
            {
                // Try to validate the repaired version
                var validation = ValidateAgainstSchema(repaired, inputSchema);
                if (validation.IsValid || validation.MissingFields.Count < GetMissingFieldCount(arguments, inputSchema))
                {
                    Log.Information("[McpNormalizer] Repair strategy {Strategy} improved arguments", strategyType);
                    return repaired;
                }
            }
        }

        return arguments;
    }

    private static int GetMissingFieldCount(JsonElement arguments, JsonElement inputSchema)
    {
        var validation = ValidateAgainstSchema(arguments, inputSchema);
        return validation.MissingFields.Count;
    }

    private static (bool IsValid, List<string> MissingFields) ValidateAgainstSchema(JsonElement arguments, JsonElement inputSchema)
    {
        var missingFields = new List<string>();

        if (inputSchema.ValueKind != JsonValueKind.Object ||
            !inputSchema.TryGetProperty("required", out var required) ||
            required.ValueKind != JsonValueKind.Array)
        {
            return (true, missingFields);
        }

        foreach (var requiredField in required.EnumerateArray())
        {
            var fieldName = requiredField.GetString();
            if (fieldName is not null && arguments.ValueKind == JsonValueKind.Object &&
                !arguments.TryGetProperty(fieldName, out _))
            {
                missingFields.Add(fieldName);
            }
        }

        return (missingFields.Count == 0, missingFields);
    }

    private static string? InferCommandFromToolName(string toolName)
    {
        // Common patterns: "azure_monitor_query" → "monitor"
        // "ado_list_pipelines" → "list"
        var parts = toolName.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            // Skip server prefix (azure, ado, platform, gitops)
            var actionParts = parts.Skip(1).ToArray();
            if (actionParts.Length > 0)
                return string.Join("_", actionParts);
        }
        return null;
    }

    private static List<string> ExtractMissingFieldsFromError(string errorMessage)
    {
        // Pattern: "Missing Required options: --resource-group, --workspace, --table"
        var fields = new List<string>();

        // Try to extract from "Missing Required options: --field1, --field2"
        var match = Regex.Match(errorMessage, @"missing\s+required\s+options?:\s*(.+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var fieldsPart = match.Groups[1].Value;
            var tokens = fieldsPart.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var cleaned = token.Trim().TrimStart('-');
                if (!string.IsNullOrWhiteSpace(cleaned))
                    fields.Add(cleaned);
            }
        }

        return fields;
    }

    /// <summary>
    /// Fix stringified JSON values within arguments.
    /// Claude sometimes sends nested objects as JSON strings instead of proper objects.
    /// e.g., {"parameters": "{\"key\":\"value\"}"} → {"parameters": {"key":"value"}}
    /// Also fixes string booleans: {"learn": "true"} → {"learn": true}
    /// </summary>
    private static JsonElement FixStringifiedJsonValues(string toolName, JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object) return arguments;

        var modified = false;
        var dict = new Dictionary<string, object?>();

        foreach (var prop in arguments.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.String)
            {
                var strVal = prop.Value.GetString();

                // Fix string booleans: "true" → true, "false" → false
                if (string.Equals(strVal, "true", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("[McpNormalizer] Tool {Tool}: converting string 'true' → boolean true in field '{Field}'", toolName, prop.Name);
                    dict[prop.Name] = true;
                    modified = true;
                    continue;
                }
                if (string.Equals(strVal, "false", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information("[McpNormalizer] Tool {Tool}: converting string 'false' → boolean false in field '{Field}'", toolName, prop.Name);
                    dict[prop.Name] = false;
                    modified = true;
                    continue;
                }

                // Fix stringified JSON objects
                if (strVal is not null && strVal.TrimStart().StartsWith('{') && strVal.TrimEnd().EndsWith('}'))
                {
                    var parsed = TryParseJsonString(strVal);
                    if (parsed.HasValue)
                    {
                        Log.Information("[McpNormalizer] Tool {Tool}: parsed stringified JSON in field '{Field}'", toolName, prop.Name);
                        dict[prop.Name] = JsonSerializer.Deserialize<object>(parsed.Value);
                        modified = true;
                        continue;
                    }
                }
            }
            dict[prop.Name] = JsonSerializer.Deserialize<object>(prop.Value);
        }

        return modified ? JsonSerializer.SerializeToElement(dict) : arguments;
    }

    /// <summary>
    /// When arguments have { "command": "X", "parameters": {parsed obj}, ... },
    /// restructure into { "parameters": { "command": "X", ...parsed fields... } }
    /// which is the format MCP servers typically expect.
    /// </summary>
    private static JsonElement RestructureWithParsedParameters(string toolName, JsonElement originalArgs, JsonElement parsedParams)
    {
        var innerDict = parsedParams.ValueKind == JsonValueKind.Object
            ? JsonSerializer.Deserialize<Dictionary<string, object?>>(parsedParams) ?? new()
            : new Dictionary<string, object?>();

        // Pull 'command' from sibling fields into the parameters object
        if (originalArgs.TryGetProperty("command", out var commandEl))
        {
            var commandStr = commandEl.ValueKind == JsonValueKind.String ? commandEl.GetString() : commandEl.GetRawText();
            if (!string.IsNullOrEmpty(commandStr))
            {
                innerDict["command"] = commandStr;
            }
        }

        // Also transfer 'intent' if present (some MCP servers use it)
        if (originalArgs.TryGetProperty("intent", out var intentEl) && intentEl.ValueKind == JsonValueKind.String)
        {
            innerDict["intent"] = intentEl.GetString();
        }

        var result = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["parameters"] = innerDict
        });

        Log.Information("[McpNormalizer] Tool {Tool}: restructured args with parsed parameters. Keys: {Keys}",
            toolName, string.Join(", ", innerDict.Keys));
        return result;
    }

    /// <summary>
    /// Attempts to parse a string as JSON, returns the parsed element or null.
    /// </summary>
    private static JsonElement? TryParseJsonString(string? jsonStr)
    {
        if (string.IsNullOrWhiteSpace(jsonStr)) return null;
        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            return doc.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}

public class RepairStrategy
{
    public RepairType Type { get; set; }
    public string Reason { get; set; } = "";
    public List<string>? MissingFields { get; set; }
}

public enum RepairType
{
    WrapInParametersRoot,
    InferCommandWrapper,
    ProvideMissingFields
}
