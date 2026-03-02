using System.Collections.Concurrent;

namespace AzureOpsCrew.Api.Background;

public class AgentScheduler : BackgroundService
{
    private readonly ConcurrentDictionary<(Guid agentId, Guid chatId), CancellationTokenSource> _jobs = new();
    private readonly IServiceProvider _serviceProvider;

    public AgentScheduler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool StartAgent(Guid agentId, Guid chatId)
    {
        var cts = new CancellationTokenSource();

        if (!_jobs.TryAdd((agentId, chatId), cts))
            return false;

        _ = Task.Run(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var agentRunService = scope.ServiceProvider.GetRequiredService<AgentRunService>();
            try
            {
                await agentRunService.Run(agentId, chatId, cts.Token);
            }
            finally
            {
                _jobs.TryRemove((agentId, chatId), out _);
                cts.Dispose();
            }
        });

        return true;
    }

    public bool StopAgent(Guid agentId, Guid chatId)
    {
        if (_jobs.TryRemove((agentId, chatId), out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            return true;
        }

        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var triggerQueue = _serviceProvider.GetRequiredService<AgentTriggerQueue>();
        while (!stoppingToken.IsCancellationRequested)
        {
            var triggers = triggerQueue.DequeueAll();
            foreach (var (agentId, chatId) in triggers)
            {
                StartAgent(agentId, chatId);
            }

            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var kv in _jobs)
        {
            await kv.Value.CancelAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
