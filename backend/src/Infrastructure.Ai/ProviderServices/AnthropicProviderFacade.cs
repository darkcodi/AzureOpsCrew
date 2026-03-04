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

            // Retry without tools when tool_use input cannot be parsed by Claudia
            Log.Warning(ex, "[ClaudiaChatClient] Tool input parse failed. Retrying without tools.");
            request.Tools = null;
            response = await _anthropic.Messages.CreateAsync(request, cancellationToken: cancellationToken);
        }
        
        Log.Warning("[ClaudiaChatClient] Claude response: StopReason={StopReason}, ContentCount={ContentCount}", 
            response.StopReason, response.Content.Count);
        
        // Process response content
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
                    // Claudia uses ToolUseId, ToolUseName, ToolUseInput for tool_use content
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
        
        // If we only have text, create a simple text response
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
                    Type = prop.Value.TryGetProperty("type", out var t) ? t.GetString() : "string",
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

    private static string? ExtractSystemPrompt(IList<ChatMessage> messages)
    {
        var systemMessage = messages.FirstOrDefault(m => m.Role == ChatRole.System);
        return systemMessage?.Text;
    }
}
