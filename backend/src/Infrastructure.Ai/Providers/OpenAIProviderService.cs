using AzureOpsCrew.Domain.Providers;
using System.Text.Json;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public sealed class OpenAIProviderService : IProviderService
{
    private readonly HttpClient _httpClient;

    public OpenAIProviderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TestConnectionAsync(ProviderConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
                ? "https://api.openai.com/v1"
                : config.ApiEndpoint;

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{endpoint}/models");
            request.Headers.Add("Authorization", $"Bearer {config.ApiKey}");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ProviderModelInfo[]> ListModelsAsync(ProviderConfig config, CancellationToken cancellationToken)
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
}
