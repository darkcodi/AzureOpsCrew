using System.ClientModel;
using AzureOpsCrew.Domain.Providers;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using AzureOpsCrew.Domain.ProviderServices;

namespace AzureOpsCrew.Infrastructure.Ai.ProviderServices;

public sealed class OpenRouterProviderFacade : IProviderFacade
{
    private const string DefaultEndpoint = "https://openrouter.ai/api/v1";
    private readonly HttpClient _httpClient;

    public OpenRouterProviderFacade(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(Provider config, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return TestConnectionResult.ValidationFailed("API key is required");
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
                ? DefaultEndpoint
                : config.ApiEndpoint.TrimEnd('/');

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/models");
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

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

            var models = modelsElement.EnumerateArray()
                .Select(m => new ProviderModelInfo(
                    m.GetProperty("id").GetString()!,
                    m.GetProperty("id").GetString()!))
                .ToArray();

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
            ? DefaultEndpoint
            : config.ApiEndpoint.TrimEnd('/');

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/models");
        request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

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

    public IChatClient CreateChatClient(Provider config, string model, CancellationToken cancellationToken)
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
