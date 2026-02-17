using AzureOpsCrew.Domain.Providers;

namespace AzureOpsCrew.Infrastructure.Ai.Providers;

public interface IProviderServiceFactory
{
    IProviderService GetService(ProviderType providerType);
}
