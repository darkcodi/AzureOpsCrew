using System.Globalization;
using System.Text.Json;
using AzureOpsCrew.Domain.Agents;
using AzureOpsCrew.Domain.Tools;
using AzureOpsCrew.Domain.Tools.BackEnd;
using AzureOpsCrew.Domain.Utils;

namespace AzureOpsCrew.Api.Background.Tools;

public class GetMessagesTool : IBackendTool
{
    public ToolDeclaration GetDeclaration()
    {
        return new ToolDeclaration
        {
            Name = "getMessages",
            Description = "Gets messages from the current channel or direct message conversation. Optionally filters to only messages posted after a given timestamp.",
            JsonSchema = JsonUtils.Schema("""
                                          {
                                            "type": "object",
                                            "properties": {
                                              "after": { "type": "string", "description": "Optional timestamp filter. Supports: ISO 8601 (e.g., 2025-01-15T10:30:00Z), Unix timestamp (seconds or milliseconds since epoch), common date formats (e.g., 2025-01-15, 01/15/2025)." }
                                            },
                                            "additionalProperties": false
                                          }
                                          """).ToString(),
            ReturnJsonSchema = JsonUtils.Schema("""
                                                {
                                                  "type": "object",
                                                  "properties": {
                                                    "messages": {
                                                      "type": "array",
                                                      "items": {
                                                        "type": "object",
                                                        "properties": {
                                                          "id": { "type": "string", "description": "The unique identifier of the message" },
                                                          "text": { "type": "string", "description": "The message text content" },
                                                          "postedAt": { "type": "string", "description": "The date and time the message was posted (ISO 8601)" },
                                                          "authorName": { "type": ["string", "null"], "description": "The name of the message author" },
                                                          "isAgentMessage": { "type": "boolean", "description": "Whether the message was posted by an agent" }
                                                        },
                                                        "required": ["id", "text", "postedAt", "isAgentMessage"],
                                                        "additionalProperties": false
                                                      },
                                                      "description": "All messages in the current conversation"
                                                    }
                                                  },
                                                  "required": ["messages"],
                                                  "additionalProperties": false
                                                }
                                                """).ToString(),
            ToolType = ToolType.BackEnd,
        };
    }

    public Task<ToolCallResult> ExecuteAsync(AgentRunData data, string callId, IDictionary<string, object?>? arguments, IServiceProvider serviceProvider)
    {
        IEnumerable<Domain.Chats.Message> source = data.ChatMessages;

        if (arguments != null && arguments.TryGetValue("after", out var afterValue))
        {
            DateTime? fromDate = TryParseDateTime(afterValue);

            if (fromDate == null)
            {
                return Task.FromResult(new ToolCallResult(callId, new { ErrorMessage = $"after param is not a valid date-time string. Failed to parse '{afterValue}'. Supported formats: ISO 8601 (e.g., 2025-01-15T10:30:00Z), Unix timestamp (seconds or milliseconds since epoch), common date formats (e.g., 2025-01-15, 01/15/2025)." }, true));
            }

            source = source.Where(m => m.PostedAt >= fromDate.Value.ToUniversalTime());
        }

        var messages = source.Select(m => new
        {
            id = m.Id,
            text = m.Text,
            postedAt = m.PostedAt.ToString("o"),
            authorName = m.AuthorName,
            isAgentMessage = m.AgentId.HasValue,
        }).ToList();

        return Task.FromResult(new ToolCallResult(callId, new { messages }, false));
    }

    public static DateTime? TryParseDateTime(object? value)
    {
        if (value is null)
            return null;

        // If already a DateTime, return it
        if (value is DateTime dt)
            return dt;

        // Handle JsonElement from JSON deserialization
        if (value is JsonElement jsonElement)
        {
            if (jsonElement.ValueKind == JsonValueKind.String)
            {
                return TryParseDateTime(jsonElement.GetString());
            }
            return null;
        }

        string? valueStr = value as string;
        if (valueStr == null)
            return null;

        // Trim whitespace
        valueStr = valueStr.Trim();

        // Try Unix timestamp (seconds since epoch)
        // Valid timestamps are typically between 1970 and 2100
        if (long.TryParse(valueStr, out var unixSeconds) && unixSeconds > 0 && unixSeconds < 4_000_000_000)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Fall through to other parsing attempts
            }
        }

        // Try Unix timestamp in milliseconds
        // Valid millisecond timestamps are typically between 1970 and 2100
        // Use >= to handle the boundary case of 10_000_000_000
        if (long.TryParse(valueStr, out var unixMillis) && unixMillis > 0 && unixMillis >= 10_000_000_000 && unixMillis < 4_000_000_000_000)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMillis).UtcDateTime;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Fall through to other parsing attempts
            }
        }

        // Try ISO 8601 with various styles
        // Only use the result if it has an explicit Kind (Utc or Local), not Unspecified
        // This prevents formats without timezone info from being treated as local time
        if (DateTime.TryParse(valueStr, null, DateTimeStyles.RoundtripKind | DateTimeStyles.AllowWhiteSpaces, out var isoResult) &&
            (isoResult.Kind == DateTimeKind.Utc || isoResult.Kind == DateTimeKind.Local))
            return isoResult;

        // Try common date formats (including .NET datetime format with fractional seconds but no timezone)
        string[] commonFormats =
        [
            "yyyy-MM-dd",
            "yyyy/MM/dd",
            "MM/dd/yyyy",
            "dd/MM/yyyy",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss",
            "MM/dd/yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-ddTHH:mm:ss.fffffff",  // .NET datetime with 7-digit fractional seconds (no timezone)
            "yyyy-MM-ddTHH:mm:ss.ffffff",   // 6-digit fractional seconds
            "yyyy-MM-ddTHH:mm:ss.fffff",    // 5-digit fractional seconds
            "yyyy-MM-ddTHH:mm:ss.ffff",     // 4-digit fractional seconds
            "yyyy-MM-ddTHH:mm:ss.fff",      // 3-digit fractional seconds
            "yyyy-MM-ddTHH:mm:ss.ff",       // 2-digit fractional seconds
            "yyyy-MM-ddTHH:mm:ss.f",        // 1-digit fractional seconds
            "yyyy-MM-ddTHH:mm:ss.fffffffZ", // 7-digit fractional seconds with Z
            "yyyy-MM-ddTHH:mm:ss.ffffffZ",  // 6-digit fractional seconds with Z
            "yyyy-MM-ddTHH:mm:ss.fffffZ",   // 5-digit fractional seconds with Z
            "yyyy-MM-ddTHH:mm:ss.ffffZ",    // 4-digit fractional seconds with Z
            "yyyy-MM-ddTHH:mm:ss.fffZ",     // 3-digit fractional seconds with Z
            "yyyy-MM-ddTHH:mm:ss.ffZ",      // 2-digit fractional seconds with Z
            "yyyy-MM-ddTHH:mm:ss.fZ",       // 1-digit fractional seconds with Z
            "o", // Round-trip date/time pattern
            "s", // Sortable date/time pattern
            "u", // Universal sortable date/time pattern
        ];

        foreach (var format in commonFormats)
        {
            if (DateTime.TryParseExact(valueStr, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
                return DateTime.SpecifyKind(result, DateTimeKind.Utc);
        }

        return null;
    }
}
