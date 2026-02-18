using AzureOpsCrew.Domain.Providers;
using Microsoft.Extensions.AI;

namespace AzureOpsCrew.Domain.ProviderServices;

public interface IProviderFacade
{
    Task<TestConnectionResult> TestConnectionAsync(Provider config, CancellationToken cancellationToken);
    Task<ProviderModelInfo[]> ListModelsAsync(Provider config, CancellationToken cancellationToken);
    IChatClient CreateChatClient(Provider config, string model, CancellationToken cancellationToken);
}
