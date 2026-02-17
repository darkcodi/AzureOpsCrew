using System.ClientModel;
using AzureOpsCrew.Domain.Providers;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public sealed class AzureFoundryProviderService : IProviderService
{
    private const string DefaultApiVersion = "2024-04-01-preview";
    private readonly HttpClient _httpClient;

    public AzureFoundryProviderService(HttpClient httpClient)
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
                ? "https://{your-resource}.openai.azure.com"
                : config.ApiEndpoint.TrimEnd('/');

            // Use /openai/models endpoint to list available base models
            // Note: Deployment names must be configured manually - they differ from model names
            var modelsUrl = $"{endpoint}/openai/models?api-version={DefaultApiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
            request.Headers.Add("api-key", config.ApiKey);

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return TestConnectionResult.AuthenticationFailed("Invalid API key");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return TestConnectionResult.ValidationFailed("Endpoint not found. Please check your Azure OpenAI resource URL.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return TestConnectionResult.NetworkError($"HTTP {response.StatusCode}: {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var modelsElement = doc.RootElement.GetProperty("data");

            var models = modelsElement.EnumerateArray()
                .Select(m => new ProviderModelInfo(
                    m.GetProperty("id").GetString()!,
                    m.GetProperty("id").GetString()!))
                .ToArray();

            // Validate deployment/model if specified
            // Note: DefaultModel should be the deployment name (e.g., "gpt-5-2-chat")
            // not the base model name (e.g., "gpt-5.2-chat-2025-12-11")
            if (!string.IsNullOrWhiteSpace(config.DefaultModel))
            {
                // For Azure OpenAI, we can't validate deployment names via the models API
                // Skip validation and assume the user has configured the correct deployment name
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
            ? "https://{your-resource}.openai.azure.com"
            : config.ApiEndpoint.TrimEnd('/');

        // Use /openai/models endpoint to list available base models
        var modelsUrl = $"{endpoint}/openai/models?api-version={DefaultApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, modelsUrl);
        request.Headers.Add("api-key", config.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var models = doc.RootElement.GetProperty("data");

        return models.EnumerateArray()
            .Select(m => new ProviderModelInfo(
                m.GetProperty("id").GetString()!,
                m.GetProperty("id").GetString()!))
            .ToArray();
    }

    public async Task<IChatClient> CreateChatClientAsync(Provider config, string model, CancellationToken cancellationToken)
    {
        var options = new AzureOpenAIClientOptions(AzureOpenAIClientOptions.ServiceVersion.V2024_06_01);
        var chatClient = new AzureOpenAIClient(
                new Uri(config.ApiEndpoint!),
                new ApiKeyCredential(config.ApiKey!),
                options)
            .GetChatClient(model);
        return chatClient.AsIChatClient();
    }
}
