using AzureOpsCrew.Domain.Providers;
using Microsoft.Extensions.DependencyInjection;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public sealed class ProviderServiceFactory : IProviderServiceFactory
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly Dictionary<ProviderType, Type> ServiceTypes = new()
    {
        { ProviderType.OpenAI, typeof(OpenAIProviderService) },
        { ProviderType.Anthropic, typeof(AnthropicProviderService) },
        { ProviderType.Ollama, typeof(OllamaProviderService) }
    };

    public ProviderServiceFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public IProviderService GetService(ProviderType providerType)
    {
        if (!ServiceTypes.ContainsKey(providerType))
            throw new ArgumentOutOfRangeException($"Provider {providerType} is not supported.");

        return (IProviderService)(_serviceProvider.GetService(ServiceTypes[providerType])
            ?? throw new InvalidOperationException($"Provider service of type {ServiceTypes[providerType]} is not registered"));
    }
}
