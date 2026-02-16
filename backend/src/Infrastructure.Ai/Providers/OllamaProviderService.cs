using AzureOpsCrew.Domain.Providers;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public sealed class OllamaProviderService : IProviderService
{
    private readonly HttpClient _httpClient;

    public OllamaProviderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(ProviderConfig config, CancellationToken cancellationToken)
    {
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

            // Validate model if specified
            if (!string.IsNullOrWhiteSpace(config.DefaultModel))
            {
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("models", out var models))
                {
                    var modelExists = models.EnumerateArray()
                        .Any(m => string.Equals(
                            m.GetProperty("model").GetString(),
                            config.DefaultModel,
                            StringComparison.Ordinal));

                    if (!modelExists)
                    {
                        return TestConnectionResult.ValidationFailed($"Model '{config.DefaultModel}' not found in available models");
                    }
                }
                else
                {
                    return TestConnectionResult.ValidationFailed($"Model '{config.DefaultModel}' not found in available models");
                }
            }

            return TestConnectionResult.Successful();
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

    public async Task<ProviderModelInfo[]> ListModelsAsync(ProviderConfig config, CancellationToken cancellationToken)
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
}
