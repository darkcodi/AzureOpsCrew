namespace AzureOpsCrew.Domain.Providers;

public interface IProviderService
{
    Task<TestConnectionResult> TestConnectionAsync(ProviderConfig config, CancellationToken cancellationToken);
    Task<ProviderModelInfo[]> ListModelsAsync(ProviderConfig config, CancellationToken cancellationToken);
}
