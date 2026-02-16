using AzureOpsCrew.Domain.Providers;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public sealed class AnthropicProviderService : IProviderService
{
    private readonly HttpClient _httpClient;

    public AnthropicProviderService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<bool> TestConnectionAsync(ProviderConfig config, CancellationToken cancellationToken)
    {
        try
        {
            var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
                ? "https://api.anthropic.com"
                : config.ApiEndpoint;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/v1/messages");
            request.Headers.Add("x-api-key", config.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent("""{"model":"claude-3-5-sonnet-20241022","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            // Will return 400 with specific error if auth works, 401 if not
            return response.StatusCode != System.Net.HttpStatusCode.Unauthorized;
        }
        catch
        {
            return false;
        }
    }

    public Task<ProviderModelInfo[]> ListModelsAsync(ProviderConfig config, CancellationToken cancellationToken)
    {
        // Anthropic has fixed models - return known list
        return Task.FromResult(new ProviderModelInfo[]
        {
            new("claude-sonnet-4-20250514", "Claude Sonnet 4", 200000),
            new("claude-3-5-sonnet-20241022", "Claude 3.5 Sonnet", 200000),
            new("claude-3-5-sonnet-20240620", "Claude 3.5 Sonnet (June)", 200000),
            new("claude-3-opus-20240229", "Claude 3 Opus", 200000),
            new("claude-3-haiku-20240307", "Claude 3 Haiku", 200000)
        });
    }
}
