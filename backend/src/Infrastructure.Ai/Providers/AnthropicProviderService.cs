using AzureOpsCrew.Domain.Providers;
using System.Text;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public sealed class AnthropicProviderService : IProviderService
{
    private static readonly string[] KnownModels =
    [
        "claude-sonnet-4-20250514",
        "claude-3-5-sonnet-20241022",
        "claude-3-5-sonnet-20240620",
        "claude-3-opus-20240229",
        "claude-3-haiku-20240307"
    ];

    private readonly HttpClient _httpClient;

    public AnthropicProviderService(HttpClient httpClient)
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

        // Validate model if specified
        if (!string.IsNullOrWhiteSpace(config.DefaultModel))
        {
            if (!KnownModels.Contains(config.DefaultModel, StringComparer.Ordinal))
            {
                return TestConnectionResult.ValidationFailed($"Model '{config.DefaultModel}' is not a valid Anthropic model");
            }
        }

        try
        {
            var endpoint = string.IsNullOrEmpty(config.ApiEndpoint)
                ? "https://api.anthropic.com"
                : config.ApiEndpoint;

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{endpoint}/v1/messages");
            request.Headers.Add("x-api-key", config.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
            request.Content = new StringContent(
                """{"model":"claude-3-5-sonnet-20241022","max_tokens":1,"messages":[{"role":"user","content":"hi"}]}""",
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);

            // Will return 400 with specific error if auth works, 401 if not
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                return TestConnectionResult.AuthenticationFailed("Invalid API key");
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
