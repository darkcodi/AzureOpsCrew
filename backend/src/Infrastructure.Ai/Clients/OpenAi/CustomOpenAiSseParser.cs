using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

/// <summary>
/// Parser for Server-Sent Events (SSE) streams from OpenAI API
/// </summary>
public static class CustomOpenAiSseParser
{
    /// <summary>
    /// Parses an SSE stream and yields OpenAI chat completion chunks
    /// </summary>
    /// <param name="stream">The input stream containing SSE data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of OpenAI chat completion chunks</returns>
    public static async IAsyncEnumerable<OpenAiChatCompletionChunk> ParseStreamAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

        StringBuilder? dataBuffer = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line == null)
            {
                // End of stream
                break;
            }

            // SSE format: lines starting with "data: " contain JSON data
            if (line.Length == 0)
            {
                // Empty line signals end of an event
                if (dataBuffer != null && dataBuffer.Length > 0)
                {
                    var data = dataBuffer.ToString();
                    dataBuffer = null;

                    var chunk = ParseDataLine(data);
                    if (chunk != null)
                    {
                        yield return chunk;
                    }
                }
                continue;
            }

            // Check if this is a comment line (starts with ':')
            if (line.StartsWith(":"))
            {
                continue;
            }

            // Check if this is a data line
            if (line.StartsWith("data: "))
            {
                var dataValue = line.Substring(6); // Skip "data: "

                if (dataValue == "[DONE]")
                {
                    // Stream termination signal
                    break;
                }

                dataBuffer ??= new StringBuilder();
                if (dataBuffer.Length > 0)
                {
                    dataBuffer.AppendLine();
                }
                dataBuffer.Append(dataValue);
            }
        }

        // Handle any remaining buffered data
        if (dataBuffer != null && dataBuffer.Length > 0)
        {
            var data = dataBuffer.ToString();
            var chunk = ParseDataLine(data);
            if (chunk != null)
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Parses a single data line into an OpenAI chat completion chunk
    /// </summary>
    private static OpenAiChatCompletionChunk? ParseDataLine(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }

        try
        {
            // Handle multi-line JSON (concatenated with newlines)
            data = data.Trim();

            // Try to parse as JSON array first (some providers return arrays)
            if (data.StartsWith('['))
            {
                var chunks = JsonSerializer.Deserialize<List<OpenAiChatCompletionChunk>>(data, JsonOptions);
                if (chunks != null)
                {
                    foreach (var chunk in chunks)
                    {
                        if (chunk != null)
                        {
                            return chunk;
                        }
                    }
                }
                return null;
            }

            // Parse as single object
            return JsonSerializer.Deserialize<OpenAiChatCompletionChunk>(data, JsonOptions);
        }
        catch (JsonException)
        {
            // Invalid JSON - skip this chunk
            return null;
        }
    }

    /// <summary>
    /// JSON serializer options for OpenAI API
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Parses an SSE stream from an HTTP response
    /// </summary>
    /// <param name="response">The HTTP response to parse</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable of OpenAI chat completion chunks</returns>
    public static async IAsyncEnumerable<OpenAiChatCompletionChunk> ParseHttpResponseAsync(
        HttpResponseMessage response,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw CreateExceptionFromResponse(response, errorContent);
        }

        await foreach (var chunk in ParseStreamAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken))
        {
            yield return chunk;
        }
    }

    /// <summary>
    /// Creates an appropriate exception from an error response
    /// </summary>
    private static Exception CreateExceptionFromResponse(HttpResponseMessage response, string content)
    {
        try
        {
            var error = JsonSerializer.Deserialize<OpenAiError>(content);
            if (error?.Error != null && !string.IsNullOrEmpty(error.Error.Message))
            {
                return new CustomOpenAiApiException(error.Error.Message, error.Error.Code)
                {
                    StatusCode = (int)response.StatusCode,
                    ErrorType = error.Error.Type
                };
            }
        }
        catch (JsonException)
        {
            // Fall through to generic exception
        }

        return new CustomOpenAiApiException(
            $"HTTP request failed with status {response.StatusCode}: {response.ReasonPhrase}",
            null)
        {
            StatusCode = (int)response.StatusCode
        };
    }
}
