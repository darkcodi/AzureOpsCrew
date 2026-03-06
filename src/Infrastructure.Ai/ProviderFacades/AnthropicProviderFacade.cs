using System.Diagnostics;
using System.Text.Json;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.ProviderFacades;

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

            // Custom models are allowed even if the provider does not report them via /models.

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
        throw new NotImplementedException();
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
