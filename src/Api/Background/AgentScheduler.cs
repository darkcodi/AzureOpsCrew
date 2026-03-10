using Serilog;

namespace AzureOpsCrew.Api.Background;

public class AgentScheduler : BackgroundService
{
    private readonly object _lock = new();
    private readonly Dictionary<(Guid agentId, Guid chatId), CancellationTokenSource> _jobs = new();
    private readonly IServiceProvider _serviceProvider;

    public AgentScheduler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool StartAgent(Guid agentId, Guid chatId)
    {
        var cts = new CancellationTokenSource();

        lock (_lock)
        {
            if (_jobs.ContainsKey((agentId, chatId)))
            {
                Log.Warning("[BACKGROUND] Agent {AgentId} for chat {ChatId} is already running", agentId, chatId);
                return false;
            }

            _jobs[(agentId, chatId)] = cts;
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
                lock (_lock)
                {
                    _jobs.Remove((agentId, chatId));
                }
                cts.Dispose();
                Log.Information("[BACKGROUND] Agent {AgentId} for chat {ChatId} stopped", agentId, chatId);
            }
        });

        return true;
    }

    public bool StopAgent(Guid agentId, Guid chatId)
    {
        CancellationTokenSource? cts = null;

        lock (_lock)
        {
            if (_jobs.TryGetValue((agentId, chatId), out cts))
            {
                _jobs.Remove((agentId, chatId));
            }
        }

        if (cts != null)
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
        CancellationTokenSource[] jobsToStop;

        lock (_lock)
        {
            jobsToStop = _jobs.Values.ToArray();
        }

        Log.Information("[BACKGROUND] Stopping {JobCount} running agent jobs", jobsToStop.Length);

        foreach (var cts in jobsToStop)
        {
            await cts.CancelAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}
