namespace AzureOpsCrew.Domain.Providers;

public interface IProviderService
{
    Task<TestConnectionResult> TestConnectionAsync(Provider config, CancellationToken cancellationToken);
    Task<ProviderModelInfo[]> ListModelsAsync(Provider config, CancellationToken cancellationToken);
}
