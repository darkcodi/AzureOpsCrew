using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureOpsCrew.Api.Orchestration;
using AzureOpsCrew.Api.Settings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Serilog;

namespace AzureOpsCrew.Api.Mcp;

/// <summary>
/// Provides MCP tools from Azure and Azure DevOps MCP servers as AIFunction instances.
/// Handles OAuth token acquisition, tool discovery, and tool invocation via MCP JSON-RPC protocol.
/// Includes retry/timeout/circuit-breaker logic and approval policy integration.
/// </summary>
public class McpToolProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly McpSettings _settings;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private List<AITool>? _azureTools;
    private List<AITool>? _adoTools;
    private List<AITool>? _platformTools;
    private List<AITool>? _gitopsTools;
    private string? _azureToken;
    private string? _adoToken;
    private string? _platformToken;
    private string? _gitopsToken;
    private DateTime _azureTokenExpiry = DateTime.MinValue;
    private DateTime _adoTokenExpiry = DateTime.MinValue;
    private DateTime _platformTokenExpiry = DateTime.MinValue;
    private DateTime _gitopsTokenExpiry = DateTime.MinValue;

    // Circuit breaker state
    private bool _azureCircuitOpen;
    private bool _adoCircuitOpen;
    private bool _platformCircuitOpen;
    private bool _gitopsCircuitOpen;
    private DateTime _azureCircuitResetTime = DateTime.MinValue;
    private DateTime _adoCircuitResetTime = DateTime.MinValue;
    private DateTime _platformCircuitResetTime = DateTime.MinValue;
    private DateTime _gitopsCircuitResetTime = DateTime.MinValue;
    private const int CircuitBreakerCooldownSeconds = 60;

    // Retry/timeout settings
    private const int MaxRetries = 2;
    private const int PerToolTimeoutSeconds = 30;
    private const int DiscoveryTimeoutSeconds = 15;

    // Per-tool failure tracking: prevents the LLM from calling the same broken tool in a loop
    private const int MaxConsecutiveToolFailures = 3;
    private readonly ConcurrentDictionary<string, int> _toolFailureCounts = new();

    /// <summary>True if Azure MCP tools were actually discovered (not mocks).</summary>
    public bool AzureToolsAvailable => _azureTools is not null && !_azureCircuitOpen;

    /// <summary>True if Azure DevOps MCP tools were actually discovered (not mocks).</summary>
    public bool AdoToolsAvailable => _adoTools is not null && !_adoCircuitOpen;

    /// <summary>True if Platform MCP tools were actually discovered (not mocks).</summary>
    public bool PlatformToolsAvailable => _platformTools is not null && !_platformCircuitOpen;

    /// <summary>True if GitOps MCP tools were actually discovered (not mocks).</summary>
    public bool GitOpsToolsAvailable => _gitopsTools is not null && !_gitopsCircuitOpen;

    public McpToolProvider(IHttpClientFactory httpClientFactory, IOptions<McpSettings> settings)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
    }

    /// <summary>
    /// Gets all Azure MCP tools as AITool instances.
    /// Falls back to mock tools for demo when MCP server is unreachable.
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
                Log.Warning("Azure MCP server URL not configured, using mock Azure tools");
                _azureTools = GetMockAzureTools();
                return _azureTools;
            }

            try
            {
                var token = await GetAzureTokenAsync(ct);
                var mcpTools = await DiscoverToolsAsync(_settings.Azure.ServerUrl, token, ct);
                _azureTools = mcpTools.Select(t => CreateAiTool(t, _settings.Azure, "azure")).ToList();
                Log.Information("Discovered {Count} Azure MCP tools", _azureTools.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to connect to Azure MCP server, using mock Azure tools");
                _azureTools = GetMockAzureTools();
            }

            return _azureTools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets all Azure DevOps MCP tools as AITool instances.
    /// Falls back to mock tools for demo when MCP server is unreachable.
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
                Log.Warning("Azure DevOps MCP server URL not configured, using mock ADO tools");
                _adoTools = GetMockAdoTools();
                return _adoTools;
            }

            try
            {
                var token = await GetAdoTokenAsync(ct);
                var mcpTools = await DiscoverToolsAsync(_settings.AzureDevOps.ServerUrl, token, ct);
                _adoTools = mcpTools.Select(t => CreateAiTool(t, _settings.AzureDevOps, "ado")).ToList();
                Log.Information("Discovered {Count} Azure DevOps MCP tools", _adoTools.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to connect to Azure DevOps MCP server, using mock ADO tools");
                _adoTools = GetMockAdoTools();
            }

            return _adoTools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets all Platform MCP tools as AITool instances.
    /// Falls back to mock tools for demo when MCP server is unreachable.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetPlatformToolsAsync(CancellationToken ct = default)
    {
        if (_platformTools is not null) return _platformTools;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_platformTools is not null) return _platformTools;

            if (string.IsNullOrWhiteSpace(_settings.Platform.ServerUrl))
            {
                Log.Warning("Platform MCP server URL not configured, using mock Platform tools");
                _platformTools = GetMockPlatformTools();
                return _platformTools;
            }

            try
            {
                var token = await GetPlatformTokenAsync(ct);
                var mcpTools = await DiscoverToolsAsync(_settings.Platform.ServerUrl, token, ct);
                _platformTools = mcpTools.Select(t => CreateAiTool(t, _settings.Platform, "platform")).ToList();
                Log.Information("Discovered {Count} Platform MCP tools", _platformTools.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to connect to Platform MCP server, using mock Platform tools");
                _platformTools = GetMockPlatformTools();
            }

            return _platformTools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets all GitOps MCP tools as AITool instances.
    /// Falls back to mock tools for demo when MCP server is unreachable.
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetGitOpsToolsAsync(CancellationToken ct = default)
    {
        if (_gitopsTools is not null) return _gitopsTools;

        await _initLock.WaitAsync(ct);
        try
        {
            if (_gitopsTools is not null) return _gitopsTools;

            if (string.IsNullOrWhiteSpace(_settings.GitOps.ServerUrl))
            {
                Log.Warning("GitOps MCP server URL not configured, using mock GitOps tools");
                _gitopsTools = GetMockGitOpsTools();
                return _gitopsTools;
            }

            try
            {
                var token = await GetGitOpsTokenAsync(ct);
                var mcpTools = await DiscoverToolsAsync(_settings.GitOps.ServerUrl, token, ct);
                _gitopsTools = mcpTools.Select(t => CreateAiTool(t, _settings.GitOps, "gitops")).ToList();
                Log.Information("Discovered {Count} GitOps MCP tools", _gitopsTools.Count);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to connect to GitOps MCP server, using mock GitOps tools");
                _gitopsTools = GetMockGitOpsTools();
            }

            return _gitopsTools;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Gets all available MCP tools (Azure + ADO + Platform + GitOps).
    /// </summary>
    public async Task<IReadOnlyList<AITool>> GetAllToolsAsync(CancellationToken ct = default)
    {
        var azureTools = await GetAzureToolsAsync(ct);
        var adoTools = await GetAdoToolsAsync(ct);
        var platformTools = await GetPlatformToolsAsync(ct);
        var gitopsTools = await GetGitOpsToolsAsync(ct);
        return [.. azureTools, .. adoTools, .. platformTools, .. gitopsTools];
    }

    /// <summary>
    /// Gets tools filtered by agent role with strict authorization enforcement.
    ///
    /// ACCESS MATRIX (from technical spec):
    /// ┌───────────┬───────────────┬─────────────┬──────────────┬──────────────┐
    /// │  Agent    │ Azure MCP     │ Platform MCP│ ADO MCP      │ GitOps MCP   │
    /// ├───────────┼───────────────┼─────────────┼──────────────┼──────────────┤
    /// │ Manager   │ read-only     │ read-only   │ read-only    │ NO ACCESS    │
    /// │ DevOps    │ read+write    │ read+write  │ read-only    │ NO ACCESS    │
    /// │ Developer │ NO ACCESS     │ NO ACCESS   │ read+ops     │ read+write   │
    /// └───────────┴───────────────┴─────────────┴──────────────┴──────────────┘
    ///
    /// Write tools are filtered based on the role:
    /// - Manager: ALL write tools removed (read-only oversight)
    /// - DevOps: GitOps tools fully blocked; ADO write tools blocked  
    /// - Developer: Azure/Platform tools fully blocked
    /// </summary>
    private const int MaxToolsPerAgent = 120;

    public async Task<IReadOnlyList<AITool>> GetToolsForAgentAsync(string agentProviderAgentId, CancellationToken ct = default)
    {
        var role = agentProviderAgentId.ToLowerInvariant();
        List<AITool> tools;

        switch (role)
        {
            case "manager":
                // Manager: read-only from Azure + Platform + ADO. No GitOps. No write tools.
                var mgrAzure = await GetAzureToolsAsync(ct);
                var mgrPlatform = await GetPlatformToolsAsync(ct);
                var mgrAdo = await GetAdoToolsAsync(ct);
                tools = [.. mgrAzure, .. mgrPlatform, .. mgrAdo];
                // Strip ALL write/dangerous tools — Manager is read-only
                tools = tools.Where(t => !ToolAuthorizationPolicy.IsWriteTool(t.Name)).ToList();
                break;

            case "devops":
                // DevOps: Azure (read+write) + Platform (read+write) + ADO (read-only). No GitOps.
                var devopsAzure = await GetAzureToolsAsync(ct);
                var devopsPlatform = await GetPlatformToolsAsync(ct);
                var devopsAdo = await GetAdoToolsAsync(ct);
                // ADO tools for DevOps are read-only — strip write
                var devopsAdoReadOnly = devopsAdo.Where(t => !ToolAuthorizationPolicy.IsWriteTool(t.Name)).ToList();
                tools = [.. devopsAzure, .. devopsPlatform, .. devopsAdoReadOnly];
                break;

            case "developer":
                // Developer: ADO (read+ops) + GitOps (read+write). NO Azure/Platform access.
                var devAdo = await GetAdoToolsAsync(ct);
                var devGitOps = await GetGitOpsToolsAsync(ct);
                tools = [.. devAdo, .. devGitOps];
                break;

            default:
                Log.Warning("Unknown agent role '{Role}', assigning NO tools for safety", role);
                tools = [];
                break;
        }

        Log.Information("Agent {Agent} has {Count} tools assigned (role-filtered)", role, tools.Count);

        if (tools.Count > MaxToolsPerAgent)
        {
            Log.Warning("Agent {Agent} has {Count} tools, exceeding OpenAI limit of {Max}. Truncating to {Max}.",
                role, tools.Count, MaxToolsPerAgent);
            tools = tools.Take(MaxToolsPerAgent).ToList();
        }

        // Secondary defense: wrap every tool with invocation-time authorization check
        tools = tools.Select(t => WrapWithAuthorizationGuard(t, role)).ToList();

        return tools;
    }

    /// <summary>
    /// Wraps an AITool with a role-based authorization guard.
    /// Even though GetToolsForAgentAsync already filters tools, this provides defense-in-depth:
    /// if a tool somehow leaks to the wrong agent, invocation is blocked and logged.
    /// </summary>
    private static AITool WrapWithAuthorizationGuard(AITool tool, string agentRole)
    {
        if (tool is not McpAiFunction mcpFunc) return tool;

        var originalInvoke = mcpFunc.InvokeAsync;

        return new McpAiFunction(
            name: mcpFunc.Name,
            description: mcpFunc.Description,
            jsonSchema: mcpFunc.InputSchema,
            invokeAsync: async (args, ct) =>
            {
                var blockReason = ToolAuthorizationPolicy.EnforceToolAccess(agentRole, mcpFunc.Name);
                if (blockReason is not null)
                {
                    Log.Error("AUTHORIZATION BLOCKED: Agent '{Role}' attempted to invoke tool '{Tool}'. Reason: {Reason}",
                        agentRole, mcpFunc.Name, blockReason);
                    return $"🚫 ACCESS DENIED: {blockReason}";
                }

                return await originalInvoke(args, ct);
            });
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

    private async Task<string> GetPlatformTokenAsync(CancellationToken ct)
    {
        if (_platformToken is not null && DateTime.UtcNow < _platformTokenExpiry)
            return _platformToken;

        var (token, expiresIn) = await AcquireTokenAsync(_settings.Platform, ct);
        _platformToken = token;
        _platformTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        return _platformToken;
    }

    private async Task<string> GetGitOpsTokenAsync(CancellationToken ct)
    {
        if (_gitopsToken is not null && DateTime.UtcNow < _gitopsTokenExpiry)
            return _gitopsToken;

        var (token, expiresIn) = await AcquireTokenAsync(_settings.GitOps, ct);
        _gitopsToken = token;
        _gitopsTokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
        return _gitopsToken;
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

        // Accept both SSE and JSON responses
        httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        httpRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        if (sessionId is not null)
            httpRequest.Headers.Add("Mcp-Session-Id", sessionId);

        var response = await client.SendAsync(httpRequest, ct);
        response.EnsureSuccessStatusCode();

        var responseSessionId = response.Headers.TryGetValues("Mcp-Session-Id", out var sessionIds)
            ? sessionIds.FirstOrDefault()
            : sessionId;

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "";

        JsonElement responseBody;
        if (contentType.Contains("event-stream", StringComparison.OrdinalIgnoreCase))
        {
            // Parse SSE: extract JSON from "data: {...}" lines
            var rawText = await response.Content.ReadAsStringAsync(ct);
            responseBody = ParseSseResponse(rawText);
        }
        else
        {
            // Plain JSON response
            var rawText = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(rawText))
            {
                // Notifications may return empty body
                responseBody = JsonSerializer.SerializeToElement(new { });
            }
            else
            {
                responseBody = JsonSerializer.Deserialize<JsonElement>(rawText);
            }
        }

        return new McpResponse { Body = responseBody, SessionId = responseSessionId };
    }

    /// <summary>
    /// Parses an SSE (Server-Sent Events) response and extracts the JSON payload from "data:" lines.
    /// Handles multi-line data fields and concatenates them.
    /// </summary>
    private static JsonElement ParseSseResponse(string sseText)
    {
        var dataLines = new List<string>();
        foreach (var line in sseText.Split('\n'))
        {
            if (line.StartsWith("data: ", StringComparison.Ordinal))
                dataLines.Add(line[6..]);
            else if (line.StartsWith("data:", StringComparison.Ordinal))
                dataLines.Add(line[5..]);
        }

        if (dataLines.Count == 0)
        {
            Log.Warning("SSE response contained no data lines, returning empty object");
            return JsonSerializer.SerializeToElement(new { });
        }

        // Concatenate all data lines (in case of multi-line data) and parse
        var jsonPayload = string.Join("", dataLines);
        return JsonSerializer.Deserialize<JsonElement>(jsonPayload);
    }

    private async Task<JsonElement> InvokeToolAsync(McpServerSettings settings, string toolName, JsonElement arguments, CancellationToken ct)
    {
        var token = settings == _settings.Azure
            ? await GetAzureTokenAsync(ct)
            : settings == _settings.Platform
                ? await GetPlatformTokenAsync(ct)
                : settings == _settings.GitOps
                    ? await GetGitOpsTokenAsync(ct)
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

    /// <summary>
    /// Detect if an MCP tool uses compound argument format (command + parameters).
    /// These tools require callers to specify a sub-command and its parameters,
    /// e.g. { "command": "query", "parameters": { "query": "..." } }
    /// </summary>
    private static bool IsCompoundTool(JsonElement? inputSchema)
    {
        if (inputSchema == null || inputSchema.Value.ValueKind != JsonValueKind.Object)
            return false;

        if (!inputSchema.Value.TryGetProperty("properties", out var props))
            return false;

        return props.TryGetProperty("command", out _) && props.TryGetProperty("parameters", out _);
    }

    /// <summary>
    /// Build enhanced description for compound MCP tools that require command + parameters format.
    /// </summary>
    private static string EnhanceCompoundToolDescription(string originalDescription, string toolName)
    {
        var sb = new System.Text.StringBuilder(originalDescription);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("IMPORTANT: This is a COMPOUND tool with multiple sub-commands.");
        sb.AppendLine("To discover available sub-commands, call with: {\"learn\": true}");
        sb.AppendLine("Then call with the specific command: {\"command\": \"<sub_command>\", \"parameters\": {<params>}}");
        sb.AppendLine("Example flow:");
        sb.AppendLine("  1. First call: {\"learn\": true} → returns list of available commands");
        sb.AppendLine("  2. Then call: {\"command\": \"query\", \"parameters\": {\"query\": \"...\"}}");
        sb.AppendLine("DO NOT pass the tool name as the command value. Use the specific sub-command from the learn response.");
        return sb.ToString();
    }

    private AITool CreateAiTool(McpToolDefinition mcpTool, McpServerSettings serverSettings, string serverPrefix)
    {
        var fullName = $"{serverPrefix}_{mcpTool.Name}";
        var baseDescription = ApprovalPolicy.EnhanceToolDescription(fullName, mcpTool.Description);
        
        // Enhance description for compound tools (command + parameters schema)
        var enhancedDescription = IsCompoundTool(mcpTool.InputSchema) 
            ? EnhanceCompoundToolDescription(baseDescription, mcpTool.Name)
            : baseDescription;

        return new McpAiFunction(
            name: fullName,
            description: enhancedDescription,
            jsonSchema: mcpTool.InputSchema,
            invokeAsync: async (args, ct) =>
            {
                var argsJson = JsonSerializer.SerializeToElement(args ?? new Dictionary<string, object?>());
                Log.Information("Invoking MCP tool {Tool} with original args: {Args}", mcpTool.Name, argsJson.ToString());

                // Per-tool failure circuit breaker: if this tool has failed too many times, reject immediately
                var failureCount = _toolFailureCounts.GetValueOrDefault(fullName, 0);
                if (failureCount >= MaxConsecutiveToolFailures)
                {
                    Log.Warning("Tool {Tool} blocked by per-tool circuit breaker ({Count} consecutive failures)", 
                        fullName, failureCount);
                    return $"⚠️ BLOCKED: Tool '{mcpTool.Name}' has failed {failureCount} consecutive times and is temporarily disabled. " +
                           "DO NOT call this tool again. Try a completely different approach or report the issue to the user.";
                }

                // Approval policy check: block dangerous tools at runtime
                if (ApprovalPolicy.RequiresApproval(fullName))
                {
                    Log.Warning("Tool {Tool} requires user approval but was called directly. Blocking execution.", fullName);
                    return $"⚠️ BLOCKED: Tool '{fullName}' is a write/destructive operation that requires explicit user approval. " +
                           "The Manager must present an approval request to the user with: action, reason, risk, rollback plan. " +
                           "Only after the user says APPROVED can this tool be called.";
                }

                // Step 1: Normalize arguments against schema
                JsonElement normalizedArgs;
                try
                {
                    normalizedArgs = McpArgumentNormalizer.NormalizeAndValidate(fullName, mcpTool.InputSchema, argsJson);
                    if (!normalizedArgs.Equals(argsJson))
                    {
                        Log.Information("MCP tool {Tool}: arguments normalized. Before: {Before}, After: {After}",
                            mcpTool.Name,
                            argsJson.ToString().Length > 200 ? argsJson.ToString()[..200] + "..." : argsJson.ToString(),
                            normalizedArgs.ToString().Length > 200 ? normalizedArgs.ToString()[..200] + "..." : normalizedArgs.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "MCP tool {Tool}: argument normalization failed, proceeding with original args", mcpTool.Name);
                    normalizedArgs = argsJson;
                }

                // Step 2: Retry with exponential backoff + MCP error-aware repair
                JsonElement currentArgs = normalizedArgs;
                for (int attempt = 0; attempt <= MaxRetries; attempt++)
                {
                    try
                    {
                        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        cts.CancelAfter(TimeSpan.FromSeconds(PerToolTimeoutSeconds));

                        var result = await InvokeToolAsync(serverSettings, mcpTool.Name, currentArgs, cts.Token);

                        // Check if result is an MCP error that we can auto-repair.
                        // Detection: explicit "error" property, OR content text containing "error",
                        // OR content text matching known MCP failure patterns (e.g. "parameters are required when not learning")
                        var resultText = FormatToolResult(result);
                        
                        // BYPASS: If result is a help/learn response (lists available commands),
                        // treat it as SUCCESS — the LLM needs this info to make correct calls.
                        if (McpArgumentNormalizer.IsLikelyHelpResponse(resultText))
                        {
                            Log.Information("MCP tool {Tool}: received help/learn response (attempt {Attempt}), returning to agent as data",
                                mcpTool.Name, attempt + 1);
                            ClearToolFailure(fullName);
                            return resultText;
                        }
                        
                        var isExplicitError = result.TryGetProperty("error", out _);
                        var isContentError = !isExplicitError && 
                            result.TryGetProperty("content", out var contentArray) && 
                            contentArray.ValueKind == JsonValueKind.Array &&
                            contentArray.EnumerateArray().Any(c => c.TryGetProperty("text", out var t) && 
                                                                     t.GetString()?.Contains("error", StringComparison.OrdinalIgnoreCase) == true);
                        var isImplicitFailure = !isExplicitError && !isContentError && 
                            McpArgumentNormalizer.IsLikelyMcpFailure(resultText);

                        if (isExplicitError || isContentError || isImplicitFailure)
                        {
                            if (isImplicitFailure)
                            {
                                Log.Warning("MCP tool {Tool}: result text matches MCP failure pattern (attempt {Attempt}): {Text}",
                                    mcpTool.Name, attempt + 1, resultText.Length > 300 ? resultText[..300] + "..." : resultText);
                            }

                            var repairStrategy = McpArgumentNormalizer.ParseErrorAndSuggestRepair(fullName, result);
                            if (repairStrategy is not null && attempt < MaxRetries)
                            {
                                Log.Information("MCP tool {Tool}: detected repairable error on attempt {Attempt}. Repair: {Reason}",
                                    mcpTool.Name, attempt + 1, repairStrategy.Reason);

                                // Apply repair and retry - pass tool name for InferCommandWrapper
                                currentArgs = McpArgumentNormalizer.ApplyRepair(repairStrategy, currentArgs, fullName);
                                Log.Information("MCP tool {Tool}: retrying with repaired args: {Args}",
                                    mcpTool.Name, currentArgs.ToString().Length > 200 ? currentArgs.ToString()[..200] + "..." : currentArgs.ToString());

                                // Retry immediately (no backoff delay for format errors)
                                continue;
                            }

                            // All repair attempts exhausted — return clear failure message
                            // so the LLM doesn't keep retrying the same broken call
                            var failureMsg = $"⚠️ TOOL CALL FAILED: '{mcpTool.Name}' returned an error after {attempt + 1} attempt(s). " +
                                             $"MCP server response: {(resultText.Length > 400 ? resultText[..400] + "..." : resultText)} " +
                                             "DO NOT retry this same tool call with the same arguments — it will fail again. " +
                                             "Try a different approach, use a different tool, or report this limitation to the user.";
                            Log.Warning("MCP tool {Tool}: all repair attempts exhausted, returning hard failure to agent", mcpTool.Name);
                            TrackToolFailure(fullName);
                            return failureMsg;
                        }

                        // Success — real data returned
                        Log.Information("MCP tool {Tool} returned (attempt {Attempt}): {Result}",
                            mcpTool.Name, attempt + 1, resultText.Length > 500 ? resultText[..500] + "..." : resultText);
                        ClearToolFailure(fullName);
                        return resultText;
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        Log.Warning("MCP tool {Tool} timed out (attempt {Attempt}/{Max})",
                            mcpTool.Name, attempt + 1, MaxRetries + 1);
                        if (attempt == MaxRetries)
                            return $"⚠️ Tool '{mcpTool.Name}' timed out after {MaxRetries + 1} attempts ({PerToolTimeoutSeconds}s each). " +
                                   "The MCP server may be slow or unresponsive. Cannot verify this data point.";
                    }
                    catch (HttpRequestException ex)
                    {
                        Log.Warning(ex, "MCP tool {Tool} HTTP error (attempt {Attempt}/{Max})",
                            mcpTool.Name, attempt + 1, MaxRetries + 1);
                        if (attempt == MaxRetries)
                        {
                            OpenCircuitBreaker(serverPrefix);
                            return $"⚠️ Tool '{mcpTool.Name}' failed after {MaxRetries + 1} attempts: {ex.Message}. " +
                                   "MCP server may be unavailable. Cannot verify this data point.";
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "MCP tool {Tool} unexpected error (attempt {Attempt}/{Max})",
                            mcpTool.Name, attempt + 1, MaxRetries + 1);
                        if (attempt == MaxRetries)
                            return $"⚠️ Error calling tool '{mcpTool.Name}': {ex.Message}. Cannot verify this data point.";
                    }

                    // Exponential backoff: 1s, 2s (for network/timeout errors, not format errors)
                    if (attempt < MaxRetries)
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                }

                return $"⚠️ Tool '{mcpTool.Name}' failed unexpectedly after {MaxRetries + 1} attempts. Cannot verify this data point.";
            });
    }

    private void OpenCircuitBreaker(string serverPrefix)
    {
        if (serverPrefix == "azure")
        {
            _azureCircuitOpen = true;
            _azureCircuitResetTime = DateTime.UtcNow.AddSeconds(CircuitBreakerCooldownSeconds);
            Log.Warning("Circuit breaker OPEN for Azure MCP. Will retry after {ResetTime}", _azureCircuitResetTime);
        }
        else if (serverPrefix == "ado")
        {
            _adoCircuitOpen = true;
            _adoCircuitResetTime = DateTime.UtcNow.AddSeconds(CircuitBreakerCooldownSeconds);
            Log.Warning("Circuit breaker OPEN for ADO MCP. Will retry after {ResetTime}", _adoCircuitResetTime);
        }
        else if (serverPrefix == "platform")
        {
            _platformCircuitOpen = true;
            _platformCircuitResetTime = DateTime.UtcNow.AddSeconds(CircuitBreakerCooldownSeconds);
            Log.Warning("Circuit breaker OPEN for Platform MCP. Will retry after {ResetTime}", _platformCircuitResetTime);
        }
        else if (serverPrefix == "gitops")
        {
            _gitopsCircuitOpen = true;
            _gitopsCircuitResetTime = DateTime.UtcNow.AddSeconds(CircuitBreakerCooldownSeconds);
            Log.Warning("Circuit breaker OPEN for GitOps MCP. Will retry after {ResetTime}", _gitopsCircuitResetTime);
        }
    }

    private void TrackToolFailure(string toolName)
    {
        var count = _toolFailureCounts.AddOrUpdate(toolName, 1, (_, c) => c + 1);
        Log.Warning("Per-tool failure tracker: {Tool} now at {Count} consecutive failures (max={Max})", 
            toolName, count, MaxConsecutiveToolFailures);
    }

    private void ClearToolFailure(string toolName)
    {
        if (_toolFailureCounts.TryRemove(toolName, out var prev) && prev > 0)
        {
            Log.Information("Per-tool failure tracker: {Tool} cleared ({Count} previous failures)", toolName, prev);
        }
    }

    /// <summary>
    /// Returns a diagnostic summary of MCP server availability for injection into agent system prompts.
    /// Helps agents know what they can and cannot check.
    /// </summary>
    public string GetAvailabilityDiagnostics()
    {
        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(_settings.Azure.ServerUrl))
        {
            lines.Add(_azureCircuitOpen
                ? "⚠️ Azure MCP: TEMPORARILY UNAVAILABLE (circuit breaker open). Read-only Azure tools may not work."
                : $"✅ Azure MCP: Available ({_azureTools?.Count ?? 0} tools loaded) — resource listing, resource details, diagnostics");
        }
        else
        {
            lines.Add("ℹ️ Azure MCP: Not configured (using mock data for demo) — resource listing, resource details");
        }

        if (!string.IsNullOrWhiteSpace(_settings.AzureDevOps.ServerUrl))
        {
            lines.Add(_adoCircuitOpen
                ? "⚠️ Azure DevOps MCP: TEMPORARILY UNAVAILABLE (circuit breaker open). ADO tools may not work."
                : $"✅ Azure DevOps MCP: Available ({_adoTools?.Count ?? 0} tools loaded) — pipelines, repos, work items");
        }
        else
        {
            lines.Add("ℹ️ Azure DevOps MCP: Not configured (using mock data for demo) — pipelines, repos, work items");
        }

        if (!string.IsNullOrWhiteSpace(_settings.Platform.ServerUrl))
        {
            lines.Add(_platformCircuitOpen
                ? "⚠️ Platform MCP: TEMPORARILY UNAVAILABLE (circuit breaker open). Platform tools may not work."
                : $"✅ Platform MCP: Available ({_platformTools?.Count ?? 0} tools loaded) — ARG queries (comprehensive resource inventory), Container Apps, Key Vault, App Insights, Log Analytics");
        }
        else
        {
            lines.Add("ℹ️ Platform MCP: Not configured (using mock data for demo) — ARG queries, Container Apps, Key Vault, App Insights");
        }

        if (!string.IsNullOrWhiteSpace(_settings.GitOps.ServerUrl))
        {
            lines.Add(_gitopsCircuitOpen
                ? "⚠️ GitOps MCP: TEMPORARILY UNAVAILABLE (circuit breaker open). GitOps tools may not work."
                : $"✅ GitOps MCP: Available ({_gitopsTools?.Count ?? 0} tools loaded) — branches, commits, PRs, pipeline triggers");
        }
        else
        {
            lines.Add("ℹ️ GitOps MCP: Not configured (using mock data for demo) — branches, commits, PRs");
        }

        lines.Add("");
        lines.Add("NOTE: For comprehensive resource inventory, DevOps should use tools from BOTH Azure MCP and Platform MCP.");

        return string.Join("\n", lines);
    }

    // Maximum characters for tool results to prevent context overflow
    // Roughly 2000 tokens × 4 chars/token = 8000 chars
    private const int MaxToolResultChars = 8000;

    private static string FormatToolResult(JsonElement result)
    {
        string rawResult;

        if (result.TryGetProperty("content", out var content))
        {
            var parts = new List<string>();
            foreach (var item in content.EnumerateArray())
            {
                if (item.TryGetProperty("text", out var text))
                    parts.Add(text.GetString() ?? "");
            }
            rawResult = string.Join("\n", parts);
        }
        else
        {
            rawResult = result.ToString();
        }

        // Truncate large results to prevent context budget overflow
        // This is critical: without truncation, large MCP results (e.g., listing many resources)
        // can cause context_length_exceeded on the next LLM call
        if (rawResult.Length > MaxToolResultChars)
        {
            var truncatedLength = MaxToolResultChars - 200; // Leave room for truncation notice
            var truncated = rawResult[..truncatedLength];

            // Try to truncate at a natural boundary (newline)
            var lastNewline = truncated.LastIndexOf('\n');
            if (lastNewline > truncatedLength / 2)
            {
                truncated = truncated[..lastNewline];
            }

            Log.Information("[MCP] Truncating large tool result from {OriginalLength} to {TruncatedLength} chars",
                rawResult.Length, truncated.Length);

            return truncated + $"\n\n[... result truncated from {rawResult.Length} to {truncated.Length} chars to fit context budget. " +
                               "If you need more data, use more specific filters or query parameters.]";
        }

        return rawResult;
    }

    #endregion

    #region Mock Tools (fallback when MCP servers are unreachable)

    private static List<AITool> GetMockAzureTools()
    {
        Log.Information("Loading mock Azure tools for demo");
        return
        [
            CreateMockTool(
                "azure_list_resources",
                "List all Azure resources in the subscription. Returns resource names, types, locations, and resource groups.",
                """{"type":"object","properties":{"resourceGroup":{"type":"string","description":"Optional resource group name to filter by"}},"required":[]}""",
                MockAzureListResources),
            CreateMockTool(
                "azure_get_resource_details",
                "Get detailed information about a specific Azure resource by name.",
                """{"type":"object","properties":{"resourceName":{"type":"string","description":"Name of the resource to look up"}},"required":["resourceName"]}""",
                MockAzureGetResourceDetails),
            CreateMockTool(
                "azure_list_resource_groups",
                "List all resource groups in the Azure subscription.",
                """{"type":"object","properties":{},"required":[]}""",
                MockAzureListResourceGroups),
        ];
    }

    private static List<AITool> GetMockAdoTools()
    {
        Log.Information("Loading mock Azure DevOps tools for demo");
        return
        [
            CreateMockTool(
                "ado_list_pipelines",
                "List all CI/CD pipelines in the Azure DevOps project. Returns pipeline names, status, and last run info.",
                """{"type":"object","properties":{"project":{"type":"string","description":"Optional project name to filter by"}},"required":[]}""",
                MockAdoListPipelines),
            CreateMockTool(
                "ado_get_pipeline_runs",
                "Get recent runs for a specific pipeline.",
                """{"type":"object","properties":{"pipelineName":{"type":"string","description":"Name of the pipeline"}},"required":["pipelineName"]}""",
                MockAdoGetPipelineRuns),
            CreateMockTool(
                "ado_list_repos",
                "List all repositories in the Azure DevOps project.",
                """{"type":"object","properties":{"project":{"type":"string","description":"Optional project name to filter by"}},"required":[]}""",
                MockAdoListRepos),
            CreateMockTool(
                "ado_list_work_items",
                "List recent work items (bugs, tasks, user stories) in the Azure DevOps project.",
                """{"type":"object","properties":{"type":{"type":"string","description":"Optional work item type filter: Bug, Task, User Story"},"state":{"type":"string","description":"Optional state filter: New, Active, Resolved, Closed"}},"required":[]}""",
                MockAdoListWorkItems),
        ];
    }

    private static List<AITool> GetMockPlatformTools()
    {
        Log.Information("Loading mock Platform tools for demo");
        return
        [
            CreateMockTool(
                "platform_arg_query_resources",
                "Execute Azure Resource Graph query. Supports KQL over all accessible resources. This is the MOST COMPREHENSIVE way to list ALL Azure resources.",
                """{"type":"object","properties":{"query":{"type":"string","description":"KQL query to execute (e.g., 'resources | project name, type, location, resourceGroup, tags')"},"subscription_id":{"type":"string","description":"Optional subscription ID to scope the query"}},"required":["query"]}""",
                MockPlatformArgQueryResources),
            CreateMockTool(
                "platform_containerapp_list",
                "List all Container Apps in a resource group.",
                """{"type":"object","properties":{"resource_group":{"type":"string","description":"Resource group name"}},"required":["resource_group"]}""",
                _ => """{"containerApps": [], "note": "Mock: Platform MCP not connected"}"""),
            CreateMockTool(
                "platform_containerapp_get",
                "Get full Container App details.",
                """{"type":"object","properties":{"name":{"type":"string","description":"Container App name"},"resource_group":{"type":"string","description":"Resource group name"}},"required":["name","resource_group"]}""",
                _ => """{"note": "Mock: Platform MCP not connected"}"""),
            CreateMockTool(
                "platform_keyvault_list_secrets_metadata",
                "List all secret names and metadata (no values) from Key Vault.",
                """{"type":"object","properties":{"vault_name":{"type":"string","description":"Key Vault name"}},"required":["vault_name"]}""",
                _ => """{"secrets": [], "note": "Mock: Platform MCP not connected"}"""),
            CreateMockTool(
                "platform_mcp_health",
                "Health check for Platform MCP server. Returns server status and tool count.",
                """{"type":"object","properties":{},"required":[]}""",
                _ => """{"status": "mock", "note": "Platform MCP not connected"}"""),
        ];
    }

    private static AITool CreateMockTool(string name, string description, string schemaJson, Func<IDictionary<string, object?>?, string> handler)
    {
        var schema = JsonSerializer.Deserialize<JsonElement>(schemaJson);
        return new McpAiFunction(
            name: name,
            description: description,
            jsonSchema: schema,
            invokeAsync: (args, ct) =>
            {
                Log.Information("Mock tool {Tool} called with args: {Args}", name,
                    args is not null ? JsonSerializer.Serialize(args) : "null");
                var result = handler(args);
                Log.Information("Mock tool {Tool} returned: {Result}", name, result.Length > 300 ? result[..300] + "..." : result);
                return Task.FromResult(result);
            });
    }

    private static string MockAzureListResources(IDictionary<string, object?>? args)
    {
        return """
        {
          "resources": [
            {"name": "ca-azure-mcp-server", "type": "Microsoft.App/containerApps", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Running"},
            {"name": "ca-azuredevops-mcp-server", "type": "Microsoft.App/containerApps", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Running"},
            {"name": "ca-azureopscrew-api", "type": "Microsoft.App/containerApps", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Running"},
            {"name": "ca-azureopscrew-frontend", "type": "Microsoft.App/containerApps", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Running"},
            {"name": "cae-azureopscrew", "type": "Microsoft.App/managedEnvironments", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Succeeded"},
            {"name": "cr-azureopscrew", "type": "Microsoft.ContainerRegistry/registries", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Succeeded"},
            {"name": "kv-azureopscrew", "type": "Microsoft.KeyVault/vaults", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Succeeded"},
            {"name": "log-azureopscrew", "type": "Microsoft.OperationalInsights/workspaces", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Succeeded"},
            {"name": "appi-azureopscrew", "type": "Microsoft.Insights/components", "location": "westus2", "resourceGroup": "rg-azureopscrew", "status": "Succeeded"}
          ],
          "totalCount": 9,
          "subscriptionId": "00000000-0000-0000-0000-000000000000"
        }
        """;
    }

    private static string MockAzureGetResourceDetails(IDictionary<string, object?>? args)
    {
        var name = (args is not null && args.TryGetValue("resourceName", out var rn) ? rn?.ToString() : null) ?? "ca-azureopscrew-api";
        return $@"{{
  ""name"": ""{name}"",
  ""type"": ""Microsoft.App/containerApps"",
  ""location"": ""westus2"",
  ""resourceGroup"": ""rg-azureopscrew"",
  ""provisioningState"": ""Succeeded"",
  ""properties"": {{
    ""managedEnvironmentId"": ""/subscriptions/.../managedEnvironments/cae-azureopscrew"",
    ""latestRevisionName"": ""{name}--revision-1"",
    ""latestRevisionFqdn"": ""{name}.orangebay-47bb5fb2.westus2.azurecontainerapps.io"",
    ""configuration"": {{""ingress"": {{""external"": true, ""targetPort"": 8080}}}},
    ""template"": {{""containers"": [{{""name"": ""{name}"", ""image"": ""cr-azureopscrew.azurecr.io/{name}:latest"", ""resources"": {{""cpu"": 0.5, ""memory"": ""1Gi""}}}}], ""scale"": {{""minReplicas"": 1, ""maxReplicas"": 3}}}}
  }}
}}";
    }

    private static string MockAzureListResourceGroups(IDictionary<string, object?>? args)
    {
        return """
        {
          "resourceGroups": [
            {"name": "rg-azureopscrew", "location": "westus2", "provisioningState": "Succeeded", "tags": {"project": "AzureOpsCrew", "hackathon": "2025"}},
            {"name": "NetworkWatcherRG", "location": "westus2", "provisioningState": "Succeeded"}
          ]
        }
        """;
    }

    private static string MockPlatformArgQueryResources(IDictionary<string, object?>? args)
    {
        // Platform MCP ARG query returns ALL resource types including those Azure MCP might miss
        return """
        {
          "count": 15,
          "data": [
            {"name": "ca-azureopscrew-api", "type": "microsoft.app/containerapps", "location": "westus2", "resourceGroup": "rg-azureopscrew", "tags": {"project": "AzureOpsCrew"}},
            {"name": "ca-azureopscrew-frontend", "type": "microsoft.app/containerapps", "location": "westus2", "resourceGroup": "rg-azureopscrew", "tags": {"project": "AzureOpsCrew"}},
            {"name": "ca-azure-mcp-server", "type": "microsoft.app/containerapps", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "ca-azuredevops-mcp-server", "type": "microsoft.app/containerapps", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "cae-azureopscrew", "type": "microsoft.app/managedenvironments", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "cr-azureopscrew", "type": "microsoft.containerregistry/registries", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "kv-azureopscrew", "type": "microsoft.keyvault/vaults", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "log-azureopscrew", "type": "microsoft.operationalinsights/workspaces", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "appi-azureopscrew", "type": "microsoft.insights/components", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "swa-azureopscrew-portal", "type": "microsoft.web/staticsites", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "sql-azureopscrew", "type": "microsoft.sql/servers", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "sqldb-azureopscrew", "type": "microsoft.sql/servers/databases", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "vnet-azureopscrew", "type": "microsoft.network/virtualnetworks", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "nsg-azureopscrew", "type": "microsoft.network/networksecuritygroups", "location": "westus2", "resourceGroup": "rg-azureopscrew"},
            {"name": "Application Insights Smart Detection", "type": "microsoft.insights/actiongroups", "location": "global", "resourceGroup": "rg-azureopscrew"}
          ],
          "subscriptionId": "00000000-0000-0000-0000-000000000000"
        }
        """;
    }

    private static string MockAdoListPipelines(IDictionary<string, object?>? args)
    {
        return """
        {
          "pipelines": [
            {"id": 1, "name": "azureopscrew-ci", "folder": "\\", "status": "enabled", "lastRun": {"id": 142, "result": "succeeded", "finishTime": "2025-03-01T10:15:00Z", "triggerInfo": {"ci.sourceBranch": "refs/heads/main"}}},
            {"id": 2, "name": "azureopscrew-cd-api", "folder": "\\deploy", "status": "enabled", "lastRun": {"id": 87, "result": "succeeded", "finishTime": "2025-03-01T10:30:00Z", "triggerInfo": {"ci.sourceBranch": "refs/heads/main"}}},
            {"id": 3, "name": "azureopscrew-cd-frontend", "folder": "\\deploy", "status": "enabled", "lastRun": {"id": 91, "result": "succeeded", "finishTime": "2025-03-01T10:32:00Z", "triggerInfo": {"ci.sourceBranch": "refs/heads/main"}}},
            {"id": 4, "name": "mcp-servers-deploy", "folder": "\\infrastructure", "status": "enabled", "lastRun": {"id": 23, "result": "succeeded", "finishTime": "2025-02-28T15:45:00Z", "triggerInfo": {"ci.sourceBranch": "refs/heads/main"}}}
          ],
          "count": 4,
          "project": "AzureOpsCrew"
        }
        """;
    }

    private static string MockAdoGetPipelineRuns(IDictionary<string, object?>? args)
    {
        var pipeline = (args is not null && args.TryGetValue("pipelineName", out var pn) ? pn?.ToString() : null) ?? "azureopscrew-ci";
        return $@"{{
  ""pipelineName"": ""{pipeline}"",
  ""runs"": [
    {{""id"": 142, ""state"": ""completed"", ""result"": ""succeeded"", ""createdDate"": ""2025-03-01T10:10:00Z"", ""finishedDate"": ""2025-03-01T10:15:00Z"", ""sourceBranch"": ""refs/heads/main"", ""sourceVersion"": ""abc1234""}},
    {{""id"": 141, ""state"": ""completed"", ""result"": ""succeeded"", ""createdDate"": ""2025-02-28T14:20:00Z"", ""finishedDate"": ""2025-02-28T14:25:00Z"", ""sourceBranch"": ""refs/heads/main"", ""sourceVersion"": ""def5678""}},
    {{""id"": 140, ""state"": ""completed"", ""result"": ""failed"", ""createdDate"": ""2025-02-27T09:00:00Z"", ""finishedDate"": ""2025-02-27T09:08:00Z"", ""sourceBranch"": ""refs/heads/feature/agents"", ""sourceVersion"": ""ghi9012""}}
  ],
  ""count"": 3
}}";
    }

    private static string MockAdoListRepos(IDictionary<string, object?>? args)
    {
        return """
        {
          "repositories": [
            {"id": "repo-1", "name": "AzureOpsCrew", "defaultBranch": "refs/heads/main", "size": 15234567, "remoteUrl": "https://dev.azure.com/azureopscrew/AzureOpsCrew/_git/AzureOpsCrew"},
            {"id": "repo-2", "name": "mcp-servers", "defaultBranch": "refs/heads/main", "size": 5432100, "remoteUrl": "https://dev.azure.com/azureopscrew/AzureOpsCrew/_git/mcp-servers"},
            {"id": "repo-3", "name": "infrastructure", "defaultBranch": "refs/heads/main", "size": 2345678, "remoteUrl": "https://dev.azure.com/azureopscrew/AzureOpsCrew/_git/infrastructure"}
          ],
          "count": 3,
          "project": "AzureOpsCrew"
        }
        """;
    }

    private static string MockAdoListWorkItems(IDictionary<string, object?>? args)
    {
        return """
        {
          "workItems": [
            {"id": 101, "type": "User Story", "title": "Multi-agent orchestration for Azure operations", "state": "Active", "assignedTo": "Ilia Tiushniakov", "priority": 1},
            {"id": 102, "type": "Task", "title": "Implement Manager agent delegation logic", "state": "Closed", "assignedTo": "Ilia Tiushniakov", "priority": 1},
            {"id": 103, "type": "Task", "title": "Connect MCP tools to Azure DevOps agent", "state": "Active", "assignedTo": "Ilia Tiushniakov", "priority": 2},
            {"id": 104, "type": "Bug", "title": "Agent response sometimes in Ukrainian instead of Russian", "state": "Closed", "assignedTo": "Ilia Tiushniakov", "priority": 2},
            {"id": 105, "type": "User Story", "title": "Discord-like chat UI for agent collaboration", "state": "Closed", "assignedTo": "Ilia Tiushniakov", "priority": 1},
            {"id": 106, "type": "Task", "title": "Deploy MCP servers to Azure Container Apps", "state": "Closed", "assignedTo": "Ilia Tiushniakov", "priority": 2}
          ],
          "count": 6,
          "project": "AzureOpsCrew"
        }
        """;
    }

    private static List<AITool> GetMockGitOpsTools()
    {
        Log.Information("Loading mock GitOps tools for demo");
        return
        [
            CreateMockTool(
                "gitops_list_repos",
                "List all repositories accessible via GitOps MCP.",
                """{"type":"object","properties":{"project":{"type":"string","description":"Optional project name"}},"required":[]}""",
                _ => """{"repositories": [{"name": "AzureOpsCrew", "defaultBranch": "main"}], "note": "Mock: GitOps MCP not connected"}"""),
            CreateMockTool(
                "gitops_get_file",
                "Read a file from a repository.",
                """{"type":"object","properties":{"repo":{"type":"string","description":"Repository name"},"path":{"type":"string","description":"File path"},"branch":{"type":"string","description":"Branch name"}},"required":["repo","path"]}""",
                _ => """{"content": "", "note": "Mock: GitOps MCP not connected"}"""),
            CreateMockTool(
                "gitops_create_branch",
                "[⚠️ WRITE] Create a new branch in a repository.",
                """{"type":"object","properties":{"repo":{"type":"string","description":"Repository name"},"branchName":{"type":"string","description":"New branch name"},"sourceBranch":{"type":"string","description":"Source branch to branch from"}},"required":["repo","branchName"]}""",
                _ => """{"success": false, "note": "Mock: GitOps MCP not connected"}"""),
            CreateMockTool(
                "gitops_commit_changes",
                "[⚠️ WRITE] Commit file changes to a branch.",
                """{"type":"object","properties":{"repo":{"type":"string","description":"Repository name"},"branch":{"type":"string","description":"Branch name"},"changes":{"type":"array","description":"Array of file changes"},"commitMessage":{"type":"string","description":"Commit message"}},"required":["repo","branch","changes","commitMessage"]}""",
                _ => """{"success": false, "note": "Mock: GitOps MCP not connected"}"""),
            CreateMockTool(
                "gitops_create_pr",
                "[⚠️ WRITE] Create a pull request.",
                """{"type":"object","properties":{"repo":{"type":"string","description":"Repository name"},"sourceBranch":{"type":"string","description":"Source branch"},"targetBranch":{"type":"string","description":"Target branch"},"title":{"type":"string","description":"PR title"},"description":{"type":"string","description":"PR description"}},"required":["repo","sourceBranch","targetBranch","title"]}""",
                _ => """{"success": false, "note": "Mock: GitOps MCP not connected"}"""),
            CreateMockTool(
                "gitops_trigger_pipeline",
                "[⚠️ WRITE] Trigger a pipeline run.",
                """{"type":"object","properties":{"pipelineId":{"type":"string","description":"Pipeline ID or name"},"branch":{"type":"string","description":"Branch to run on"}},"required":["pipelineId"]}""",
                _ => """{"success": false, "note": "Mock: GitOps MCP not connected"}"""),
        ];
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

    /// <summary>Expose InputSchema for tool wrapping (used by authorization guard).</summary>
    public JsonElement InputSchema => _jsonSchema;

    /// <summary>Expose the invoke delegate for tool wrapping (used by authorization guard).</summary>
    public new Func<IDictionary<string, object?>?, CancellationToken, Task<string>> InvokeAsync => _invokeAsync;

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
