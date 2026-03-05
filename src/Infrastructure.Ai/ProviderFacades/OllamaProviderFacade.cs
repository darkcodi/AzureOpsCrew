using System.Diagnostics;
using System.Text.Json;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.ProviderFacades;

public sealed class OllamaProviderFacade : IProviderFacade
{
    private readonly HttpClient _httpClient;

    public OllamaProviderFacade(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(Provider config, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
                ? "http://localhost:11434"
                : config.ApiEndpoint;

            var response = await _httpClient.GetAsync(
                $"{endpoint}/api/tags",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return TestConnectionResult.NetworkError($"HTTP {response.StatusCode}: {response.ReasonPhrase}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("models", out var modelsElement))
            {
                stopwatch.Stop();
                return TestConnectionResult.Successful(stopwatch.ElapsedMilliseconds, []);
            }

            var models = modelsElement.EnumerateArray()
                .Select(m => new ProviderModelInfo(
                    m.GetProperty("model").GetString()!,
                    m.GetProperty("name").GetString()!,
                    m.TryGetProperty("details", out var details) &&
                     details.TryGetProperty("context_length", out var ctx)
                        ? ctx.GetInt64() : null))
                .ToArray();

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
            ? "http://localhost:11434"
            : config.ApiEndpoint;

        var response = await _httpClient.GetAsync(
            $"{endpoint}/api/tags",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var models = doc.RootElement.GetProperty("models");

        return models.EnumerateArray()
            .Select(m => new ProviderModelInfo(
                m.GetProperty("model").GetString()!,
                m.GetProperty("name").GetString()!,
                m.TryGetProperty("details", out var details) &&
                 details.TryGetProperty("context_length", out var ctx)
                    ? ctx.GetInt64() : null))
            .ToArray();
    }

    public IChatClient CreateChatClient(Provider config, string model, CancellationToken cancellationToken)
    {
        var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
            ? "http://localhost:11434/v1"
            : config.ApiEndpoint.TrimEnd('/') + "/v1";

        // Ollama provides an OpenAI-compatible API endpoint at /v1
        // Use dummy API key as it's required by OpenAIClient but ignored by Ollama
        var options = new CustomOpenAiChatClientOptions(new Uri(endpoint), "ollama", model);
        var chatClient = new CustomOpenAiChatClient(options);

        return chatClient;
    }
}
