namespace AzureOpsCrew.Domain.Providers;

public interface IProviderServiceFactory
{
    IProviderService GetService(ProviderType providerType);
}
