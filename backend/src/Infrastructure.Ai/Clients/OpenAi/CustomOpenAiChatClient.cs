using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;

public sealed class CustomOpenAiChatClient : IChatClient
{
    private readonly CustomOpenAiChatClientOptions _options;
    private readonly HttpClient _httpClient;

    public const string HttpClientRole = "HTTP_CLIENT";

    public CustomOpenAiChatClient(CustomOpenAiChatClientOptions options, HttpClient? httpClient = null)
    {
        _options = options;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var request = OpenAiRequestMapper.MapToOpenAiRequest(messages, options, _options.Model, stream: false);
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);

        using var httpRequest = await CreateHttpRequestAsync(requestJson, cancellationToken);
        using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseContentRead, cancellationToken);

        await EnsureSuccessAsync(httpResponse, cancellationToken);

        var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
        var response = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(responseJson, JsonOptions);

        if (response == null)
        {
            throw new OpenAiApiException("Failed to deserialize OpenAI response", null);
        }

        var chatResponse = OpenAiResponseMapper.ToChatResponse(response);

        // Add request and response JSON as system messages for traceability
        chatResponse.Messages.Insert(0, new ChatMessage(new ChatRole(HttpClientRole), requestJson));
        chatResponse.Messages.Add(new ChatMessage(new ChatRole(HttpClientRole), responseJson));

        return chatResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = OpenAiRequestMapper.MapToOpenAiRequest(messages, options, _options.Model, stream: true);
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        yield return new ChatResponseUpdate(new ChatRole(HttpClientRole), requestJson);

        using var httpRequest = await CreateHttpRequestAsync(requestJson, cancellationToken);

        // Use ResponseHeadersRead to start streaming immediately
        using var httpResponse = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        await EnsureSuccessAsync(httpResponse, cancellationToken);

        var responseStream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken);

        var toolCallBuilder = new OpenAiStreamToolCallBuilder();

        var responseJsonBuilder = new StringBuilder();
        var isReasoning = false;
        await foreach (var (data, chunk) in OpenAiSseParser.ParseStreamAsync(responseStream, cancellationToken))
        {
            responseJsonBuilder.AppendLine(data);
            responseJsonBuilder.AppendLine();
            if (chunk != null && chunk.Choices.Count > 0)
            {
                var update = OpenAiResponseMapper.ToChatResponseUpdate(chunk, ref isReasoning, toolCallBuilder);
                yield return update;

                // Check if stream is complete
                if (chunk.Choices[0].FinishReason != null)
                {
                    // Add any remaining incomplete tool calls at end of stream
                    var allCalls = toolCallBuilder.GetAllCalls(includeIncomplete: true);
                    foreach (var call in allCalls)
                    {
                        if (!string.IsNullOrEmpty(call.Id) && !string.IsNullOrEmpty(call.Function?.Name))
                        {
                            // Parse arguments JSON to dictionary
                            Dictionary<string, object?>? argumentsDict = null;
                            if (!string.IsNullOrEmpty(call.Function.Arguments))
                            {
                                try
                                {
                                    argumentsDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(call.Function.Arguments);
                                }
                                catch (JsonException)
                                {
                                    argumentsDict = new Dictionary<string, object?>();
                                }
                            }

                            yield return new ChatResponseUpdate(ChatRole.Assistant, [new FunctionCallContent(
                                call.Id,
                                call.Function.Name ?? string.Empty,
                                argumentsDict ?? new Dictionary<string, object?>())])
                            {
                                MessageId = chunk.Id,
                                ModelId = chunk.Model
                            };
                        }
                    }
                }
            }
        }

        yield return new ChatResponseUpdate(new ChatRole(HttpClientRole), responseJsonBuilder.ToString());
    }

    // Why this method even exist in the IChatClient interface? Looks like a poor design.
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    /// <summary>
    /// Creates an HttpRequestMessage with proper headers
    /// </summary>
    private async Task<HttpRequestMessage> CreateHttpRequestAsync(string requestJson, CancellationToken ct)
    {
        var endpoint = GetEndpoint();

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        // Set headers
        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            httpRequest.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
        }

        if (!string.IsNullOrEmpty(_options.OrganizationId))
        {
            httpRequest.Headers.Add("OpenAI-Organization", _options.OrganizationId);
        }

        if (!string.IsNullOrEmpty(_options.ProjectId))
        {
            httpRequest.Headers.Add("OpenAI-Project", _options.ProjectId);
        }

        if (!string.IsNullOrEmpty(_options.AzureAudience))
        {
            httpRequest.Headers.Add("ms-azure-ai-audience", _options.AzureAudience);
        }

        if (!string.IsNullOrEmpty(_options.UserAgentApplicationId))
        {
            httpRequest.Headers.Add("User-Agent", _options.UserAgentApplicationId);
        }

        // Azure-specific headers
        if (!string.IsNullOrEmpty(_options.ApiKey) && IsAzureEndpoint())
        {
            httpRequest.Headers.Add("api-key", _options.ApiKey);
        }

        return httpRequest;
    }

    /// <summary>
    /// Gets the full endpoint URL for chat completions
    /// </summary>
    private string GetEndpoint()
    {
        var endpoint = _options.Endpoint.ToString().TrimEnd('/');

        // Handle different endpoint formats
        // OpenAI: https://api.openai.com/v1
        // Azure: https://<resource>.openai.azure.com/openai/deployments/<deployment>

        if (IsAzureEndpoint())
        {
            // Azure OpenAI format
            // Expected: https://<resource>.openai.azure.com
            // Appends: /openai/deployments/<model>/chat/completions?api-version=...

            var version = _options.AzureVersion ?? "2024-06-01";
            var model = _options.Model;

            // Check if endpoint already includes deployment path
            if (endpoint.Contains("/deployments/", StringComparison.OrdinalIgnoreCase))
            {
                return $"{endpoint}/chat/completions?api-version={version}";
            }

            return $"{endpoint}/openai/deployments/{model}/chat/completions?api-version={version}";
        }

        // Standard OpenAI format
        // Expected: https://api.openai.com/v1 or similar
        // Appends: /chat/completions

        return $"{endpoint}/chat/completions";
    }

    /// <summary>
    /// Checks if the endpoint is an Azure OpenAI endpoint
    /// </summary>
    private bool IsAzureEndpoint()
    {
        var host = _options.Endpoint.Host.ToLowerInvariant();
        return host.Contains(".openai.azure.com") ||
               host.Contains(".azure-api.net") ||
               host.Contains("azure.openai.") ||
               _options.AzureAudience != null;
    }

    /// <summary>
    /// Ensures the HTTP response is successful or throws an appropriate exception
    /// </summary>
    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        // Try to parse as OpenAI error format
        try
        {
            var errorResponse = JsonSerializer.Deserialize<OpenAiError>(content);
            if (errorResponse?.Error != null)
            {
                throw OpenAiApiException.FromErrorResponse(errorResponse, (int)response.StatusCode);
            }
        }
        catch (JsonException)
        {
            // Not an OpenAI error response
        }

        // Generic error
        throw new OpenAiApiException(
            $"OpenAI API request failed with status {response.StatusCode}: {response.ReasonPhrase}. {content}",
            null)
        {
            StatusCode = (int)response.StatusCode
        };
    }

    /// <summary>
    /// JSON serializer options for OpenAI API
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };
}

public class CustomOpenAiChatClientOptions
{
    public CustomOpenAiChatClientOptions(Uri endpoint, string apiKey, string model)
    {
        Endpoint = endpoint;
        ApiKey = apiKey;
        Model = model;
    }

    // Must-have options
    public Uri Endpoint { get; set; } = new Uri("https://api.openai.com/v1");
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    // Optional generic OpenAI options
    public string? OrganizationId { get; set; }
    public string? ProjectId { get; set; }
    public string? UserAgentApplicationId { get; set; }

    // Optional Azure-specific options
    public string? AzureAudience { get; set; }
    public string? AzureVersion { get; set; } // V2024_06_01
}
