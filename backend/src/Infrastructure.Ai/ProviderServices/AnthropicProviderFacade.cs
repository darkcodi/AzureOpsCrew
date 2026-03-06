using AzureOpsCrew.Domain.Providers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.AI;
using AzureOpsCrew.Domain.ProviderServices;
using Claudia;
using Serilog;

namespace AzureOpsCrew.Infrastructure.Ai.ProviderServices;

public sealed class AnthropicProviderFacade : IProviderFacade
{
    private readonly HttpClient _httpClient;

    public AnthropicProviderFacade(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(Provider config, CancellationToken cancellationToken)
    {
        // Validate API key
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return TestConnectionResult.ValidationFailed("API key is required");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
                ? "https://api.anthropic.com/v1"
                : config.ApiEndpoint.TrimEnd('/');

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/models");
            request.Headers.Add("x-api-key", config.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return TestConnectionResult.AuthenticationFailed("Invalid API key");
            }

            if (!response.IsSuccessStatusCode)
            {
                return TestConnectionResult.NetworkError($"HTTP {response.StatusCode}: {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var modelsElement = doc.RootElement.GetProperty("data");

            var models = ParseModels(modelsElement);

            // Validate model if specified
            if (!string.IsNullOrWhiteSpace(config.DefaultModel))
            {
                var modelExists = models.Any(m => string.Equals(
                    m.Id,
                    config.DefaultModel,
                    StringComparison.Ordinal));

                if (!modelExists)
                {
                    return TestConnectionResult.ValidationFailed($"Model '{config.DefaultModel}' not found in available models");
                }
            }

            stopwatch.Stop();
            return TestConnectionResult.Successful(stopwatch.ElapsedMilliseconds, models);
        }
        catch (HttpRequestException ex)
        {
            return TestConnectionResult.NetworkError(ex.Message);
        }
        catch (TaskCanceledException)
        {
            return TestConnectionResult.Timeout();
        }
        catch (Exception ex)
        {
            return TestConnectionResult.UnknownError(ex.Message);
        }
    }

    public async Task<ProviderModelInfo[]> ListModelsAsync(Provider config, CancellationToken cancellationToken)
    {
        var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
            ? "https://api.anthropic.com/v1"
            : config.ApiEndpoint.TrimEnd('/');

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/models");
        request.Headers.Add("x-api-key", config.ApiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var modelsElement = doc.RootElement.GetProperty("data");
        return ParseModels(modelsElement);
    }

    public IChatClient CreateChatClient(Provider config, string model, CancellationToken cancellationToken)
    {
        var anthropic = new Claudia.Anthropic
        {
            ApiKey = config.ApiKey!
        };
        
        return new ClaudiaChatClient(anthropic, model);
    }

    private static ProviderModelInfo[] ParseModels(JsonElement modelsElement)
    {
        var result = new List<ProviderModelInfo>();

        foreach (var model in modelsElement.EnumerateArray())
        {
            var id = model.GetProperty("id").GetString()!;
            var displayName = model.TryGetProperty("display_name", out var nameElement)
                ? nameElement.GetString()!
                : id;

            long? contextSize = null;
            if (model.TryGetProperty("context_window_size", out var ctxElement))
            {
                contextSize = ctxElement.GetInt64();
            }

            result.Add(new ProviderModelInfo(id, displayName, contextSize));
        }

        return [.. result];
    }
}

/// <summary>
/// Adapter to bridge Claudia's Anthropic API with Microsoft.Extensions.AI.IChatClient
/// FIXED: Handles consecutive assistant messages to prevent "prefill" errors
/// </summary>
internal sealed class ClaudiaChatClient : IChatClient
{
    private readonly Claudia.Anthropic _anthropic;
    private readonly string _model;

    public ClaudiaChatClient(Claudia.Anthropic anthropic, string model)
    {
        _anthropic = anthropic;
        _model = model;
    }

    public ChatClientMetadata Metadata => new(nameof(ClaudiaChatClient), null, _model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var messagesList = chatMessages.ToList();
        var messages = ConvertMessages(messagesList);
        var systemPrompt = ExtractSystemPrompt(messagesList);
        
        // CRITICAL DEBUG: Log tool availability
        var toolCount = options?.Tools?.Count ?? 0;
        Log.Warning("[ClaudiaChatClient] GetResponseAsync called: {MessageCount} messages, {ToolCount} tools in options", 
            messagesList.Count, toolCount);
        
        if (toolCount > 0)
        {
            var toolNames = options!.Tools!.Take(5).Select(t => t.Name).ToList();
            Log.Warning("[ClaudiaChatClient] First 5 tools: {Tools}", string.Join(", ", toolNames));
        }
        else
        {
            Log.Error("[ClaudiaChatClient] NO TOOLS provided in ChatOptions! Agent will not have any tools.");
        }
        
        var request = new MessageRequest
        {
            Model = _model,
            MaxTokens = options?.MaxOutputTokens ?? 8192,
            Messages = messages
        };
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            request.System = systemPrompt;
        }
        
        // Convert and add tools to the request
        if (options?.Tools is { Count: > 0 })
        {
            try
            {
                var tools = ConvertTools(options.Tools);
                if (tools is { Length: > 0 })
                {
                    request.Tools = tools;
                    Log.Warning("[ClaudiaChatClient] Added {ToolCount} tools to Claude request", tools.Length);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[ClaudiaChatClient] Failed to convert tools - this is critical!");
            }
        }
        
        Log.Warning("[ClaudiaChatClient] Sending request to Claude. Tools in request: {HasTools}", request.Tools?.Length ?? 0);
        
        MessageResponse response;
        try
        {
            response = await _anthropic.Messages.CreateAsync(request, cancellationToken: cancellationToken);
        }
        catch (JsonException ex) when (ex.Message.Contains("DictionaryJsonConverter", StringComparison.Ordinal))
        {
            if (request.Tools is null)
            {
                throw;
            }

            // Claudia's DictionaryJsonConverter fails on tool_use inputs with nested objects.
            // Instead of retrying WITHOUT tools (which makes the agent useless),
            // we use raw HTTP to get the response and parse tool calls ourselves.
            Log.Warning(ex, "[ClaudiaChatClient] Claudia DictionaryJsonConverter failed. Falling back to raw HTTP API for tool_use parsing.");
            return await FallbackRawApiCallAsync(request, cancellationToken);
        }
        
        Log.Warning("[ClaudiaChatClient] Claude response: StopReason={StopReason}, ContentCount={ContentCount}", 
            response.StopReason, response.Content.Count);
        
        return BuildChatResponse(response);
    }
    
    /// <summary>
    /// Build a ChatResponse from Claudia's MessageResponse.
    /// </summary>
    private static ChatResponse BuildChatResponse(MessageResponse response)
    {
        var contentList = new List<AIContent>();
        var hasToolUse = false;
        
        foreach (var contentBlock in response.Content)
        {
            if (contentBlock.Type == "text" && !string.IsNullOrEmpty(contentBlock.Text))
            {
                contentList.Add(new TextContent(contentBlock.Text));
            }
            else if (contentBlock.Type == "tool_use")
            {
                hasToolUse = true;
                try
                {
                    var toolId = contentBlock.ToolUseId ?? Guid.NewGuid().ToString();
                    var toolName = contentBlock.ToolUseName ?? "unknown";
                    var toolInputObj = contentBlock.ToolUseInput as IDictionary<string, string>;
                    var toolInput = toolInputObj != null ? JsonSerializer.Serialize(toolInputObj) : "{}";
                    
                    Log.Warning("[ClaudiaChatClient] Tool call extracted: name={Tool}, id={Id}, input={Input}", 
                        toolName, toolId, toolInput.Substring(0, Math.Min(200, toolInput.Length)));
                    
                    var args = !string.IsNullOrEmpty(toolInput)
                        ? JsonSerializer.Deserialize<Dictionary<string, object?>>(toolInput)
                        : new Dictionary<string, object?>();
                    
                    contentList.Add(new FunctionCallContent(toolId, toolName, args));
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[ClaudiaChatClient] Failed to parse tool_use content");
                }
            }
        }
        
        if (contentList.Count == 0)
        {
            contentList.Add(new TextContent(""));
        }
        
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contentList))
        {
            ModelId = response.Model,
            FinishReason = hasToolUse ? ChatFinishReason.ToolCalls : response.StopReason switch
            {
                "end_turn" => ChatFinishReason.Stop,
                "max_tokens" => ChatFinishReason.Length,
                "stop_sequence" => ChatFinishReason.Stop,
                "tool_use" => ChatFinishReason.ToolCalls,
                _ => null
            },
            Usage = new UsageDetails
            {
                InputTokenCount = response.Usage.InputTokens,
                OutputTokenCount = response.Usage.OutputTokens,
                TotalTokenCount = response.Usage.InputTokens + response.Usage.OutputTokens
            }
        };
    }
    
    /// <summary>
    /// Fallback: call Anthropic API via raw HTTP when Claudia's DictionaryJsonConverter fails.
    /// This bypasses Claudia's deserialization and parses tool_use inputs as JsonElement (not Dictionary&lt;string,string&gt;).
    /// </summary>
    private async Task<ChatResponse> FallbackRawApiCallAsync(MessageRequest request, CancellationToken ct)
    {
        var apiKey = _anthropic.ApiKey;
        var endpoint = "https://api.anthropic.com/v1/messages";
        
        // Build the request body manually
        var body = new JsonObject
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens
        };
        
        if (!string.IsNullOrEmpty(request.System))
        {
            body["system"] = request.System;
        }
        
        // Serialize messages
        var messagesArray = new JsonArray();
        foreach (var msg in request.Messages)
        {
            messagesArray.Add(new JsonObject
            {
                ["role"] = msg.Role,
                ["content"] = msg.Content?.FirstOrDefault()?.Text ?? ""
            });
        }
        body["messages"] = messagesArray;
        
        // Serialize tools
        if (request.Tools is { Length: > 0 })
        {
            var toolsArray = new JsonArray();
            foreach (var tool in request.Tools)
            {
                var toolObj = new JsonObject
                {
                    ["name"] = tool.Name,
                    ["description"] = tool.Description ?? tool.Name
                };
                
                // Serialize InputSchema
                var schemaObj = new JsonObject { ["type"] = "object" };
                if (tool.InputSchema?.Properties != null)
                {
                    var propsObj = new JsonObject();
                    foreach (var kvp in tool.InputSchema.Properties)
                    {
                        var propObj = new JsonObject();
                        if (kvp.Value.Type != null) propObj["type"] = kvp.Value.Type;
                        if (kvp.Value.Description != null) propObj["description"] = kvp.Value.Description;
                        propsObj[kvp.Key] = propObj;
                    }
                    schemaObj["properties"] = propsObj;
                }
                if (tool.InputSchema?.Required is { Length: > 0 })
                {
                    var reqArr = new JsonArray();
                    foreach (var r in tool.InputSchema.Required)
                        reqArr.Add(r);
                    schemaObj["required"] = reqArr;
                }
                toolObj["input_schema"] = schemaObj;
                toolsArray.Add(toolObj);
            }
            body["tools"] = toolsArray;
        }
        
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Add("x-api-key", apiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");
        httpRequest.Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json");
        
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        using var httpResponse = await httpClient.SendAsync(httpRequest, ct);
        
        var responseJson = await httpResponse.Content.ReadAsStringAsync(ct);
        
        if (!httpResponse.IsSuccessStatusCode)
        {
            Log.Error("[ClaudiaChatClient] Raw API call failed: {StatusCode} {Body}",
                httpResponse.StatusCode, responseJson.Length > 500 ? responseJson[..500] : responseJson);
            throw new HttpRequestException($"Anthropic API error: {httpResponse.StatusCode}");
        }
        
        // Parse response manually - this handles tool_use inputs as JsonElement (no DictionaryJsonConverter)
        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;
        
        var model = root.TryGetProperty("model", out var m) ? m.GetString() ?? _model : _model;
        var stopReason = root.TryGetProperty("stop_reason", out var sr) ? sr.GetString() : "end_turn";
        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            inputTokens = usage.TryGetProperty("input_tokens", out var it) ? it.GetInt32() : 0;
            outputTokens = usage.TryGetProperty("output_tokens", out var ot) ? ot.GetInt32() : 0;
        }
        
        var contentList = new List<AIContent>();
        var hasToolUse = false;
        
        if (root.TryGetProperty("content", out var contentArray) && contentArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var block in contentArray.EnumerateArray())
            {
                var type = block.TryGetProperty("type", out var bt) ? bt.GetString() : null;
                
                if (type == "text")
                {
                    var text = block.TryGetProperty("text", out var txt) ? txt.GetString() : null;
                    if (!string.IsNullOrEmpty(text))
                        contentList.Add(new TextContent(text));
                }
                else if (type == "tool_use")
                {
                    hasToolUse = true;
                    var toolId = block.TryGetProperty("id", out var tid) ? tid.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();
                    var toolName = block.TryGetProperty("name", out var tn) ? tn.GetString() ?? "unknown" : "unknown";
                    
                    // Parse input as raw JSON - this is the key difference from Claudia's DictionaryJsonConverter
                    var args = new Dictionary<string, object?>();
                    if (block.TryGetProperty("input", out var inputElement) && inputElement.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in inputElement.EnumerateObject())
                        {
                            args[prop.Name] = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString(),
                                JsonValueKind.Number => prop.Value.GetRawText(),
                                JsonValueKind.True => "true",
                                JsonValueKind.False => "false",
                                JsonValueKind.Null => null,
                                // For nested objects/arrays, serialize to string
                                _ => prop.Value.GetRawText()
                            };
                        }
                    }
                    
                    var inputStr = JsonSerializer.Serialize(args);
                    Log.Warning("[ClaudiaChatClient] [RawFallback] Tool call: name={Tool}, id={Id}, input={Input}",
                        toolName, toolId, inputStr.Length > 200 ? inputStr[..200] : inputStr);
                    
                    contentList.Add(new FunctionCallContent(toolId, toolName, args));
                }
            }
        }
        
        Log.Warning("[ClaudiaChatClient] [RawFallback] Parsed response: StopReason={StopReason}, ContentCount={Count}, HasToolUse={HasToolUse}",
            stopReason, contentList.Count, hasToolUse);
        
        if (contentList.Count == 0)
            contentList.Add(new TextContent(""));
        
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contentList))
        {
            ModelId = model,
            FinishReason = hasToolUse ? ChatFinishReason.ToolCalls : stopReason switch
            {
                "end_turn" => ChatFinishReason.Stop,
                "max_tokens" => ChatFinishReason.Length,
                "stop_sequence" => ChatFinishReason.Stop,
                "tool_use" => ChatFinishReason.ToolCalls,
                _ => null
            },
            Usage = new UsageDetails
            {
                InputTokenCount = inputTokens,
                OutputTokenCount = outputTokens,
                TotalTokenCount = inputTokens + outputTokens
            }
        };
    }
    
    // Helper methods to extract tool_use properties via reflection/dynamic
    private static string? GetToolId(Content content)
    {
        var prop = content.GetType().GetProperty("Id");
        return prop?.GetValue(content)?.ToString();
    }
    
    private static string? GetToolName(Content content)
    {
        var prop = content.GetType().GetProperty("Name");
        return prop?.GetValue(content)?.ToString();
    }
    
    private static string? GetToolInput(Content content)
    {
        var prop = content.GetType().GetProperty("Input");
        var input = prop?.GetValue(content);
        if (input == null) return "{}";
        return input is string s ? s : JsonSerializer.Serialize(input);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For now, simulate streaming by returning the full response as a single update
        var response = await GetResponseAsync(chatMessages, options, cancellationToken);
        
        var toolCalls = response.Messages.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToList();
        if (toolCalls.Count > 0)
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, toolCalls.Cast<AIContent>().ToList())
            {
                FinishReason = ChatFinishReason.ToolCalls,
                ModelId = response.ModelId
            };
        }
        else
        {
            yield return new ChatResponseUpdate(ChatRole.Assistant, [new TextContent(response.Text)])
            {
                FinishReason = response.FinishReason,
                ModelId = response.ModelId
            };
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(IChatClient))
            return this;
        return null;
    }

    public void Dispose() { }

    /// <summary>
    /// Convert messages ensuring Claude API requirements are met:
    /// - No consecutive messages with the same role
    /// - Conversation must end with a user message (unless we're continuing tool use)
    /// </summary>
    private static Message[] ConvertMessages(IList<ChatMessage> messages)
    {
        var result = new List<Message>();
        string? lastRole = null;
        
        foreach (var m in messages)
        {
            if (m.Role == ChatRole.System) continue;
            
            var role = m.Role == ChatRole.Assistant ? "assistant" : "user";
            var text = m.Text ?? string.Empty;
            
            // Handle tool results - they need special formatting as text
            var toolResults = m.Contents.OfType<FunctionResultContent>().ToList();
            if (toolResults.Count > 0)
            {
                // Tool results go as user messages with the result as text
                var resultTexts = toolResults.Select(tr => 
                    $"[Tool Result for {tr.CallId}]: {tr.Result?.ToString() ?? "null"}")
                    .ToList();
                var resultContent = string.Join("\n\n", resultTexts);
                
                result.Add(new Message
                {
                    Role = "user",
                    Content = resultContent
                });
                lastRole = "user";
                continue;
            }
            
            // If same role as last message, merge them
            if (role == lastRole && result.Count > 0)
            {
                var lastMsg = result[^1];
                var existingText = lastMsg.Content?.FirstOrDefault()?.Text ?? "";
                var authorPrefix = m.AuthorName != null ? $"[{m.AuthorName}]: " : "";
                
                result[^1] = new Message
                {
                    Role = role,
                    Content = $"{existingText}\n\n{authorPrefix}{text}"
                };
                continue;
            }
            
            // Add message with author name for multi-agent context
            var messageText = m.AuthorName != null && m.Role == ChatRole.Assistant 
                ? $"[{m.AuthorName}]: {text}"
                : text;
            
            result.Add(new Message
            {
                Role = role,
                Content = messageText
            });
            lastRole = role;
        }
        
        // Claude requires conversation to end with user message
        // If it ends with assistant, add a minimal user prompt
        if (result.Count > 0 && result[^1].Role == "assistant")
        {
            result.Add(new Message
            {
                Role = "user",
                Content = "Please continue with your response."
            });
            Log.Debug("[ClaudiaChatClient] Added continuation prompt to fix assistant-ending conversation");
        }
        
        // Ensure we have at least one message
        if (result.Count == 0)
        {
            result.Add(new Message
            {
                Role = "user",
                Content = "Hello"
            });
        }
        
        return result.ToArray();
    }

    /// <summary>
    /// Convert Microsoft.Extensions.AI.AITool to Claudia Tool format
    /// </summary>
    private static Tool[]? ConvertTools(IList<AITool>? tools)
    {
        if (tools is null or { Count: 0 }) return null;
        
        var result = new List<Tool>();
        var maxTools = 120; // Claude has a limit
        var count = 0;
        
        foreach (var tool in tools)
        {
            if (count >= maxTools) break;
            
            if (tool is AIFunction aiFunc)
            {
                try
                {
                    // Extract schema from AIFunction.JsonSchema
                    var inputSchema = CreateInputSchema(aiFunc.JsonSchema);
                    
                    result.Add(new Tool
                    {
                        Name = aiFunc.Name,
                        Description = aiFunc.Description ?? aiFunc.Name,
                        InputSchema = inputSchema
                    });
                    count++;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "[ClaudiaChatClient] Failed to convert tool {Tool}", tool.Name);
                }
            }
        }
        
        Log.Information("[ClaudiaChatClient] Converted {Count} tools for Claude", result.Count);
        return result.Count > 0 ? result.ToArray() : null;
    }
    
    /// <summary>
    /// Create InputSchema from AIFunction's JsonSchema
    /// </summary>
    private static InputSchema CreateInputSchema(JsonElement schema)
    {
        var inputSchema = new InputSchema
        {
            Type = "object"
        };
        
        // Extract properties if available
        if (schema.ValueKind == JsonValueKind.Object && 
            schema.TryGetProperty("properties", out var properties))
        {
            var propsDict = new Dictionary<string, ToolProperty>();
            foreach (var prop in properties.EnumerateObject())
            {
                propsDict[prop.Name] = new ToolProperty
                {
                    Type = ExtractTypeString(prop.Value),
                    Description = prop.Value.TryGetProperty("description", out var desc) ? desc.GetString() : null
                };
            }
            inputSchema.Properties = propsDict;
        }
        
        // Extract required fields if available
        if (schema.ValueKind == JsonValueKind.Object &&
            schema.TryGetProperty("required", out var required))
        {
            var reqList = new List<string>();
            foreach (var req in required.EnumerateArray())
            {
                // Handle both string and array elements in required field
                if (req.ValueKind == JsonValueKind.String)
                {
                    var reqStr = req.GetString();
                    if (!string.IsNullOrEmpty(reqStr))
                    {
                        reqList.Add(reqStr);
                    }
                }
                else if (req.ValueKind == JsonValueKind.Array)
                {
                    // Skip array elements (tool schema may have nested arrays)
                    continue;
                }
            }
            inputSchema.Required = reqList.ToArray();
        }
        
        return inputSchema;
    }
    
    /// <summary>
    /// Extract type string from a JSON Schema property.
    /// Handles both simple types ("string") and nullable types (["string", "null"]).
    /// </summary>
    private static string ExtractTypeString(JsonElement propSchema)
    {
        if (!propSchema.TryGetProperty("type", out var typeElement))
            return "string";

        // Simple string type: "type": "string"
        if (typeElement.ValueKind == JsonValueKind.String)
            return typeElement.GetString() ?? "string";

        // Nullable type array: "type": ["string", "null"]  →  pick the non-null type
        if (typeElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in typeElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var val = item.GetString();
                    if (val != null && !val.Equals("null", StringComparison.OrdinalIgnoreCase))
                        return val;
                }
            }
            return "string"; // fallback if all entries are "null"
        }

        return "string";
    }

    private static string? ExtractSystemPrompt(IList<ChatMessage> messages)
    {
        var systemMessage = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        return systemMessage?.Text;
    }
}
