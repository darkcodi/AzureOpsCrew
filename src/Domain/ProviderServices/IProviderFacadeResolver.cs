using AzureOpsCrew.Domain.Providers;

namespace AzureOpsCrew.Domain.ProviderServices;

public interface IProviderFacadeResolver
{
    IProviderFacade GetService(ProviderType providerType);
}
