using System.Diagnostics;
using System.Text.Json;
using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;
using AzureOpsCrew.Infrastructure.Ai.Clients.OpenAi;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Infrastructure.Ai.ProviderFacades;

public sealed class OpenAIProviderFacade : IProviderFacade
{
    private readonly HttpClient _httpClient;

    public OpenAIProviderFacade(HttpClient httpClient)
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
                ? "https://api.openai.com/v1"
                : config.ApiEndpoint;

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
            ? "https://api.openai.com/v1"
            : config.ApiEndpoint;

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
        var options = new CustomOpenAiChatClientOptions(new Uri(config.ApiEndpoint!), config.ApiKey!, model)
        {
            ProviderName = config.Name,
            ProviderType = config.ProviderType.ToString(),
            ForceReasoningCompatibility = model.Contains("deepseek", StringComparison.OrdinalIgnoreCase)
        };
        var chatClient = new CustomOpenAiChatClient(options);

        return chatClient;
    }
}
