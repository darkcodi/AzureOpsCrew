using AzureOpsCrew.Domain.Providers;
using System.Diagnostics;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public sealed class AzureFoundryProviderService : IProviderService
{
    private const string DefaultApiVersion = "2024-04-01-preview";
    private readonly HttpClient _httpClient;

    public AzureFoundryProviderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(ProviderConfig config, CancellationToken cancellationToken)
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

            // Azure OpenAI uses /openai/deployments endpoint with api-version query param
            var deploymentsUrl = $"{endpoint}/openai/deployments?api-version={DefaultApiVersion}";

            using var request = new HttpRequestMessage(HttpMethod.Get, deploymentsUrl);
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
            var deploymentsElement = doc.RootElement.GetProperty("data");

            var models = deploymentsElement.EnumerateArray()
                .Select(d => new ProviderModelInfo(
                    d.GetProperty("id").GetString()!,
                    d.GetProperty("id").GetString()!))
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
                    return TestConnectionResult.ValidationFailed($"Deployment '{config.DefaultModel}' not found in available deployments");
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

    public async Task<ProviderModelInfo[]> ListModelsAsync(ProviderConfig config, CancellationToken cancellationToken)
    {
        var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
            ? "https://{your-resource}.openai.azure.com"
            : config.ApiEndpoint.TrimEnd('/');

        var deploymentsUrl = $"{endpoint}/openai/deployments?api-version={DefaultApiVersion}";

        using var request = new HttpRequestMessage(HttpMethod.Get, deploymentsUrl);
        request.Headers.Add("api-key", config.ApiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);

        var deployments = doc.RootElement.GetProperty("data");

        return deployments.EnumerateArray()
            .Select(d => new ProviderModelInfo(
                d.GetProperty("id").GetString()!,
                d.GetProperty("id").GetString()!))
            .ToArray();
    }
}
