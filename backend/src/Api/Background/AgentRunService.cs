namespace AzureOpsCrew.Api.Background;

public class AgentRunService
{
    private readonly IServiceProvider _serviceProvider;

    public AgentRunService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task Run(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // TODO: Implement the actual agent logic here. This is just a placeholder to keep the service running.
            await Task.Delay(TimeSpan.FromSeconds(1));
        }
    }
}
