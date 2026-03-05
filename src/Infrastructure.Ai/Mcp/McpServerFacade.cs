using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AzureOpsCrew.Domain.McpServerConfigurations;

namespace AzureOpsCrew.Infrastructure.Ai.Mcp;

public sealed class McpServerFacade
{
    private const string ProtocolVersion = "2025-11-05";
    private const string SessionHeaderName = "MCP-Session-Id";
    private const string ProtocolHeaderName = "MCP-Protocol-Version";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;

    public McpServerFacade(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<McpServerToolConfiguration[]> GetAvailableToolsAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("MCP server URL is required.", nameof(url));

        var endpoint = url.Trim();
        var requestId = 1;
        string? sessionId = null;
        var negotiatedProtocolVersion = ProtocolVersion;

        try
        {
            var initializeResponse = await SendRequestAsync(
                endpoint,
                new
                {
                    jsonrpc = "2.0",
                    id = requestId++,
                    method = "initialize",
                    @params = new
                    {
                        protocolVersion = ProtocolVersion,
                        capabilities = new { },
                        clientInfo = new
                        {
                            name = "AzureOpsCrew",
                            version = "1.0.0"
                        }
                    }
                },
                sessionId,
                protocolVersion: null,
                cancellationToken);

            sessionId = initializeResponse.SessionId;

            if (initializeResponse.Result.TryGetProperty("protocolVersion", out var protocolVersionElement)
                && protocolVersionElement.ValueKind == JsonValueKind.String)
            {
                negotiatedProtocolVersion = protocolVersionElement.GetString() ?? ProtocolVersion;
            }

            await SendNotificationAsync(
                endpoint,
                new
                {
                    jsonrpc = "2.0",
                    method = "notifications/initialized"
                },
                sessionId,
                negotiatedProtocolVersion,
                cancellationToken);

            var tools = new List<McpServerToolConfiguration>();
            string? cursor = null;

            do
            {
                object parameters = cursor is null
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?> { ["cursor"] = cursor };

                var response = await SendRequestAsync(
                    endpoint,
                    new
                    {
                        jsonrpc = "2.0",
                        id = requestId++,
                        method = "tools/list",
                        @params = parameters
                    },
                    sessionId,
                    negotiatedProtocolVersion,
                    cancellationToken);

                if (!response.Result.TryGetProperty("tools", out var toolsElement) || toolsElement.ValueKind != JsonValueKind.Array)
                    break;

                foreach (var toolElement in toolsElement.EnumerateArray())
                {
                    if (!toolElement.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
                        continue;

                    var tool = new McpServerToolConfiguration(nameElement.GetString()!)
                    {
                        Description = toolElement.TryGetProperty("description", out var descriptionElement) && descriptionElement.ValueKind == JsonValueKind.String
                            ? descriptionElement.GetString()
                            : null,
                        InputSchemaJson = toolElement.TryGetProperty("inputSchema", out var inputSchemaElement) && inputSchemaElement.ValueKind != JsonValueKind.Null
                            ? inputSchemaElement.GetRawText()
                            : null,
                        OutputSchemaJson = toolElement.TryGetProperty("outputSchema", out var outputSchemaElement) && outputSchemaElement.ValueKind != JsonValueKind.Null
                            ? outputSchemaElement.GetRawText()
                            : null,
                    };

                    tools.Add(tool);
                }

                cursor = response.Result.TryGetProperty("nextCursor", out var nextCursorElement) && nextCursorElement.ValueKind == JsonValueKind.String
                    ? nextCursorElement.GetString()
                    : null;
            }
            while (!string.IsNullOrWhiteSpace(cursor));

            return tools
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => x.First())
                .ToArray();
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(sessionId))
                await TryDeleteSessionAsync(endpoint, sessionId, cancellationToken);
        }
    }

    private async Task<McpJsonRpcResponse> SendRequestAsync(
        string url,
        object payload,
        string? sessionId,
        string? protocolVersion,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, url, payload, sessionId, protocolVersion);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await ReadJsonPayloadAsync(response, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"MCP request failed with status {(int)response.StatusCode}: {content}");

        using var document = JsonDocument.Parse(content);
        var root = GetResponseRoot(document.RootElement);

        if (root.TryGetProperty("error", out var errorElement))
            throw new InvalidOperationException($"MCP returned error: {errorElement.GetRawText()}");

        if (!root.TryGetProperty("result", out var resultElement))
            throw new InvalidOperationException("MCP response did not contain a result.");

        return new McpJsonRpcResponse(
            resultElement.Clone(),
            TryGetHeaderValue(response.Headers, SessionHeaderName));
    }

    private async Task SendNotificationAsync(
        string url,
        object payload,
        string? sessionId,
        string protocolVersion,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, url, payload, sessionId, protocolVersion);
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"MCP notification failed with status {(int)response.StatusCode}: {content}");
    }

    private async Task TryDeleteSessionAsync(string url, string sessionId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Add(SessionHeaderName, sessionId);

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            _ = response.StatusCode;
        }
        catch
        {
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, object payload, string? sessionId, string? protocolVersion)
    {
        var request = new HttpRequestMessage(method, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json")
        };

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        if (!string.IsNullOrWhiteSpace(sessionId))
            request.Headers.Add(SessionHeaderName, sessionId);

        if (!string.IsNullOrWhiteSpace(protocolVersion))
            request.Headers.Add(ProtocolHeaderName, protocolVersion);

        return request;
    }

    private static JsonElement GetResponseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            return root;

        foreach (var item in root.EnumerateArray())
            return item;

        throw new InvalidOperationException("MCP response array was empty.");
    }

    private static async Task<string> ReadJsonPayloadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var mediaType = response.Content.Headers.ContentType?.MediaType;

        if (!string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
            return content;

        var normalized = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        var blocks = normalized.Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        foreach (var block in blocks)
        {
            var dataLines = block.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                .Select(line => line[5..].TrimStart())
                .ToArray();

            if (dataLines.Length == 0)
                continue;

            var data = string.Join("\n", dataLines);
            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                continue;

            return data;
        }

        throw new InvalidOperationException("MCP SSE response did not contain a JSON payload.");
    }

    private static string? TryGetHeaderValue(HttpResponseHeaders headers, string headerName)
    {
        return headers.TryGetValues(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
    }

    private sealed record McpJsonRpcResponse(JsonElement Result, string? SessionId);
}
