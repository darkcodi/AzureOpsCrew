using AzureOpsCrew.Domain.Orchestration;
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

    public bool StartAgent(AgentTrigger trigger)
    {
        var cts = new CancellationTokenSource();
        var key = (trigger.AgentId, trigger.ChatId);

        if (!_jobs.TryAdd(key, cts))
        {
            Log.Debug("[BACKGROUND] Agent {AgentId} for chat {ChatId} is already running (trigger: {TriggerKind})",
                trigger.AgentId, trigger.ChatId, trigger.Kind);
            return false;
        }

        Log.Information("[BACKGROUND] Starting agent {AgentId} for chat {ChatId} (trigger: {TriggerKind}, taskId: {TaskId})",
            trigger.AgentId, trigger.ChatId, trigger.Kind, trigger.TaskId);

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var agentRunService = scope.ServiceProvider.GetRequiredService<AgentRunService>();
                await agentRunService.Run(trigger, cts.Token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[BACKGROUND] Error running agent {AgentId} for chat {ChatId}", trigger.AgentId, trigger.ChatId);
            }
            finally
            {
                _jobs.TryRemove(key, out _);
                cts.Dispose();
                Log.Information("[BACKGROUND] Agent {AgentId} for chat {ChatId} stopped", trigger.AgentId, trigger.ChatId);
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

            foreach (var trigger in triggers)
            {
                var started = StartAgent(trigger);
                if (!started)
                {
                    // Keep trigger for later if the agent is currently busy.
                    triggerQueue.Enqueue(trigger);
                }
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
