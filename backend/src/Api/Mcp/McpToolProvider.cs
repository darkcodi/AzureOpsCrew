using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpsCrew.Api.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Serilog;

namespace AzureOpsCrew.Api.Mcp;

/// <summary>
/// Provides MCP tools from Azure and Azure DevOps MCP servers as AIFunction instances.
/// Handles OAuth token acquisition, tool discovery, and tool invocation via MCP JSON-RPC protocol.
/// </summary>
public class McpToolProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly McpSettings _settings;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private List<AITool>? _azureTools;
    private List<AITool>? _adoTools;
    private string? _azureToken;
    private string? _adoToken;
    private DateTime _azureTokenExpiry = DateTime.MinValue;
    private DateTime _adoTokenExpiry = DateTime.MinValue;

    public McpToolProvider(IHttpClientFactory httpClientFactory, IOptions<McpSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    /// <summary>
    /// Gets all Azure MCP tools as AITool instances.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetAzureToolsAsync(CancellationToken ct = default)
    {
        if (_azureTools is not null) return _azureTools;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_azureTools is not null) return _azureTools;

            if (string.IsNullOrWhiteSpace(_settings.Azure.ServerUrl))
            {
                Log.Warning("Azure MCP server URL not configured, no Azure tools available");
                _azureTools = [];
                return _azureTools;
            }

            var token = await GetAzureTokenAsync(ct);
            var mcpTools = await DiscoverToolsAsync(_settings.Azure.ServerUrl, token, ct);
            _azureTools = mcpTools.Select(t => CreateAiTool(t, _settings.Azure, "azure")).ToList();
            Log.Information("Discovered {Count} Azure MCP tools", _azureTools.Count);
            return _azureTools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets all Azure DevOps MCP tools as AITool instances.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetAdoToolsAsync(CancellationToken ct = default)
    {
        if (_adoTools is not null) return _adoTools;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_adoTools is not null) return _adoTools;

            if (string.IsNullOrWhiteSpace(_settings.AzureDevOps.ServerUrl))
            {
                Log.Warning("Azure DevOps MCP server URL not configured, no ADO tools available");
                _adoTools = [];
                return _adoTools;
            }

            var token = await GetAdoTokenAsync(ct);
            var mcpTools = await DiscoverToolsAsync(_settings.AzureDevOps.ServerUrl, token, ct);
            _adoTools = mcpTools.Select(t => CreateAiTool(t, _settings.AzureDevOps, "ado")).ToList();
            Log.Information("Discovered {Count} Azure DevOps MCP tools", _adoTools.Count);
            return _adoTools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets all available MCP tools (Azure + ADO).
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetAllToolsAsync(CancellationToken ct = default)
    {
        var azureTools = await GetAzureToolsAsync(ct);
        var adoTools = await GetAdoToolsAsync(ct);
        return [.. azureTools, .. adoTools];
    }

    /// <summary>
    /// Gets tools filtered by agent role.
    /// Manager gets all tools, Azure Dev gets Azure tools, Azure DevOps gets ADO tools.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetToolsForAgentAsync(string agentProviderAgentId, CancellationToken ct = default)
    {
        return agentProviderAgentId.ToLowerInvariant() switch
        {
            "manager" => await GetAllToolsAsync(ct),
            "azure-dev" => await GetAzureToolsAsync(ct),
            "azure-devops" => await GetAdoToolsAsync(ct),
            _ => await GetAllToolsAsync(ct)
        };
    }

    #region OAuth Token Acquisition

    private async Task<string> GetAzureTokenAsync(CancellationToken ct)
    {
        if (_azureToken is not null && DateTime.UtcNow < _azureTokenExpiry)
            return _azureToken;

        var (token, expiresIn) = await AcquireTokenAsync(_settings.Azure, ct);
        _azureToken = token;
        _azureTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // Refresh 1 min early
        return _azureToken;
    }

    private async Task<string> GetAdoTokenAsync(CancellationToken ct)
    {
        if (_adoToken is not null && DateTime.UtcNow < _adoTokenExpiry)
            return _adoToken;

        var (token, expiresIn) = await AcquireTokenAsync(_settings.AzureDevOps, ct);
        _adoToken = token;
        _adoTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        return _adoToken;
    }

    private async Task<(string Token, int ExpiresIn)> AcquireTokenAsync(McpServerSettings settings, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = settings.ClientId,
            ["client_secret"] = settings.ClientSecret,
            ["scope"] = settings.Scope,
        });

        var response = await client.PostAsync(settings.TokenUrl, content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        var token = json.GetProperty("access_token").GetString()!;
        var expiresIn = json.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        Log.Information("Acquired OAuth token for MCP server {Url}, expires in {ExpiresIn}s", settings.ServerUrl, expiresIn);
        return (token, expiresIn);
    }

    #endregion

    #region MCP Protocol

    private async Task<List<McpToolDefinition>> DiscoverToolsAsync(string serverUrl, string token, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // First: initialize the MCP session
        var initRequest = new JsonRpcRequest
        {
            Method = "initialize",
            Id = "init-1",
            Params = new JsonElement?(JsonSerializer.SerializeToElement(new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new { name = "AzureOpsCrew", version = "1.0" }
            }))
        };

        var initResponse = await SendJsonRpcAsync(client, serverUrl, initRequest, ct);
        var sessionId = initResponse.SessionId;
        Log.Information("MCP session initialized with server {Url}, sessionId: {SessionId}", serverUrl, sessionId);

        // Send initialized notification
        var initializedNotification = new JsonRpcRequest
        {
            Method = "notifications/initialized",
            Id = null
        };
        await SendJsonRpcAsync(client, serverUrl, initializedNotification, ct, sessionId);

        // Now list tools
        var listRequest = new JsonRpcRequest
        {
            Method = "tools/list",
            Id = "tools-list-1",
        };

        var listResponse = await SendJsonRpcAsync(client, serverUrl, listRequest, ct, sessionId);

        var tools = new List<McpToolDefinition>();
        if (listResponse.Body.TryGetProperty("result", out var result) &&
            result.TryGetProperty("tools", out var toolsArray))
        {
            foreach (var tool in toolsArray.EnumerateArray())
            {
                var name = tool.GetProperty("name").GetString() ?? "";
                var description = tool.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
                var inputSchema = tool.TryGetProperty("inputSchema", out var schema) ? schema : default;

                tools.Add(new McpToolDefinition
                {
                    Name = name,
                    Description = description,
                    InputSchema = inputSchema
                });
            }
        }

        return tools;
    }

    private async Task<McpResponse> SendJsonRpcAsync(HttpClient client, string serverUrl, JsonRpcRequest request, CancellationToken ct, string? sessionId = null)
    {
        var jsonContent = JsonSerializer.Serialize(request, JsonRpcSerializerOptions);
        var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, serverUrl)
        {
            Content = httpContent
        };

        if (sessionId is not null)
            httpRequest.Headers.Add("Mcp-Session-Id", sessionId);

        var response = await client.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var responseSessionId = response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds)
            ? sessionIds.FirstOrDefault()
            : sessionId;

        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        return new McpResponse { Body = responseBody, SessionId = responseSessionId };
    }

    private async Task<JsonElement> InvokeToolAsync(McpServerSettings settings, string toolName, JsonElement arguments, CancellationToken ct)
    {
        var token = settings == _settings.Azure
            ? await GetAzureTokenAsync(ct)
            : await GetAdoTokenAsync(ct);

        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Initialize session for tool call
        var initRequest = new JsonRpcRequest
        {
            Method = "initialize",
            Id = $"init-tool-{Guid.NewGuid():N}",
            Params = new JsonElement?(JsonSerializer.SerializeToElement(new
            {
                protocolVersion = "2025-03-26",
                capabilities = new { },
                clientInfo = new { name = "AzureOpsCrew", version = "1.0" }
            }))
        };

        var initResponse = await SendJsonRpcAsync(client, settings.ServerUrl, initRequest, ct);
        var sessionId = initResponse.SessionId;

        // Send initialized notification
        var initializedNotification = new JsonRpcRequest
        {
            Method = "notifications/initialized",
            Id = null
        };
        await SendJsonRpcAsync(client, settings.ServerUrl, initializedNotification, ct, sessionId);

        // Call the tool
        var callRequest = new JsonRpcRequest
        {
            Method = "tools/call",
            Id = $"call-{Guid.NewGuid():N}",
            Params = new JsonElement?(JsonSerializer.SerializeToElement(new
            {
                name = toolName,
                arguments = arguments
            }))
        };

        var response = await SendJsonRpcAsync(client, settings.ServerUrl, callRequest, ct, sessionId);

        if (response.Body.TryGetProperty("result", out var result))
            return result;

        if (response.Body.TryGetProperty("error", out var error))
        {
            Log.Error("MCP tool {ToolName} returned error: {Error}", toolName, error.ToString());
            return error;
        }

        return response.Body;
    }

    #endregion

    #region AITool Creation

    private AITool CreateAiTool(McpToolDefinition mcpTool, McpServerSettings serverSettings, string serverPrefix)
    {
        return new McpAiFunction(
            name: $"{serverPrefix}_{mcpTool.Name}",
            description: mcpTool.Description,
            jsonSchema: mcpTool.InputSchema,
            invokeAsync: async (args, ct) =>
            {
                var argsJson = JsonSerializer.SerializeToElement(args ?? new Dictionary<string, object?>());
                Log.Information("Invoking MCP tool {Tool} with args: {Args}", mcpTool.Name, argsJson.ToString());

                try
                {
                    var result = await InvokeToolAsync(serverSettings, mcpTool.Name, argsJson, ct);
                    var resultStr = FormatToolResult(result);
                    Log.Information("MCP tool {Tool} returned: {Result}", mcpTool.Name, resultStr.Length > 500 ? resultStr[..500] + "..." : resultStr);
                    return resultStr;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error invoking MCP tool {Tool}", mcpTool.Name);
                    return $"Error calling tool {mcpTool.Name}: {ex.Message}";
                }
            });
    }

    private static string FormatToolResult(JsonElement result)
    {
        if (result.TryGetProperty("content", out var content))
        {
            var parts = new List<string>();
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text))
                    parts.Add(text.GetString() ?? "");
            }
            return string.Join("\n", parts);
        }

        return result.ToString();
    }

    #endregion

    private static readonly JsonSerializerOptions JsonRpcSerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}

#region Internal Types

internal class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Jsonrpc { get; set; } = "2.0";

    [JsonPropertyName("method")]
    public string Method { get; set; } = "";

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

internal class McpResponse
{
    public JsonElement Body { get; set; }
    public string? SessionId { get; set; }
}

internal class McpToolDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public JsonElement InputSchema { get; set; }
}

/// <summary>
/// Custom AIFunction implementation that wraps MCP tool calls.
/// Extends AIFunction to provide proper Name, Description, JsonSchema, and invocable behavior.
/// </summary>
internal class McpAiFunction : AIFunction
{
    private readonly Func<IDictionary<string, object?>?, CancellationToken, Task<string>> _invokeAsync;
    private readonly string _name;
    private readonly string _description;
    private readonly JsonElement _jsonSchema;

    public McpAiFunction(
        string name,
        string description,
        JsonElement jsonSchema,
        Func<IDictionary<string, object?>?, CancellationToken, Task<string>> invokeAsync)
    {
        _name = name;
        _description = description;
        _jsonSchema = jsonSchema;
        _invokeAsync = invokeAsync;
    }

    public override string Name => _name;
    public override string Description => _description;
    public override JsonElement JsonSchema => _jsonSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // Convert AIFunctionArguments (IDictionary<string, object?>) to a plain dictionary for MCP call
        var dict = new Dictionary<string, object?>();
        foreach (var kvp in arguments)
            dict[kvp.Key] = kvp.Value;

        return await _invokeAsync(dict, cancellationToken);
    }
}

#endregion
