namespace AzureOpsCrew.Domain.Providers;

public interface IProviderService
{
    Task<bool> TestConnectionAsync(ProviderConfig config, CancellationToken cancellationToken);
    Task<ProviderModelInfo[]> ListModelsAsync(ProviderConfig config, CancellationToken cancellationToken);
}
