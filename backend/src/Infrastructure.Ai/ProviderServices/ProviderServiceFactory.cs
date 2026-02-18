using AzureOpsCrew.Domain.Providers;
using AzureOpsCrew.Domain.ProviderServices;

namespace AzureOpsCrew.Infrastructure.Ai.ProviderServices;

public sealed class ProviderFacadeResolver : IProviderFacadeResolver
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly Dictionary<ProviderType, Type> ServiceTypes = new()
    {
        { ProviderType.Anthropic, typeof(AnthropicProviderFacade) },
        { ProviderType.AzureFoundry, typeof(AzureFoundryProviderFacade) },
        { ProviderType.Ollama, typeof(OllamaProviderFacade) },
        { ProviderType.OpenAI, typeof(OpenAIProviderFacade) },
        { ProviderType.OpenRouter, typeof(OpenRouterProviderFacade) },
    };

    public ProviderFacadeResolver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IProviderFacade GetService(ProviderType providerType)
    {
        if (!ServiceTypes.ContainsKey(providerType))
            throw new ArgumentOutOfRangeException($"Provider {providerType} is not supported.");

        return (IProviderFacade)(_serviceProvider.GetService(ServiceTypes[providerType])
            ?? throw new InvalidOperationException($"Provider service of type {ServiceTypes[providerType]} is not registered"));
    }
}
