using Serilog;
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
        {
            Log.Warning("[BACKGROUND] Agent {AgentId} for chat {ChatId} is already running", agentId, chatId);
            return false;
        }

        Log.Information("[BACKGROUND] Starting agent {AgentId} for chat {ChatId}", agentId, chatId);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var agentRunService = scope.ServiceProvider.GetRequiredService<AgentRunService>();
                await agentRunService.Run(agentId, chatId, cts.Token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[BACKGROUND] Error running agent {AgentId} for chat {ChatId}", agentId, chatId);
            }
            finally
            {
                _jobs.TryRemove((agentId, chatId), out _);
                cts.Dispose();
                Log.Information("[BACKGROUND] Agent {AgentId} for chat {ChatId} stopped", agentId, chatId);
            }
        });

        return true;
    }

    public bool StopAgent(Guid agentId, Guid chatId)
    {
        if (_jobs.TryRemove((agentId, chatId), out var cts))
        {
            Log.Information("[BACKGROUND] Stopping agent {AgentId} for chat {ChatId}", agentId, chatId);
            cts.Cancel();
            cts.Dispose();
            return true;
        }

        Log.Warning("[BACKGROUND] Agent {AgentId} for chat {ChatId} was not running", agentId, chatId);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var triggerQueue = _serviceProvider.GetRequiredService<AgentTriggerQueue>();
        Log.Information("[BACKGROUND] AgentScheduler started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var triggers = triggerQueue.DequeueAll();
            if (triggers.Count > 0)
            {
                Log.Debug("[BACKGROUND] Dequeued {TriggerCount} agent triggers", triggers.Count);
            }

            foreach (var (agentId, chatId) in triggers)
            {
                StartAgent(agentId, chatId);
            }

            await Task.Delay(1000, stoppingToken);
        }

        Log.Information("[BACKGROUND] AgentScheduler stopping");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Log.Information("[BACKGROUND] Stopping {JobCount} running agent jobs", _jobs.Count);

        foreach (var kv in _jobs)
        {
            await kv.Value.CancelAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
