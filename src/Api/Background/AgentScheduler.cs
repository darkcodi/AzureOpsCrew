using AzureOpsCrew.Domain.Triggers;
using AzureOpsCrew.Domain.WaitConditions;
using AzureOpsCrew.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AzureOpsCrew.Api.Background;

public class AgentScheduler : BackgroundService
{
    private readonly object _lock = new();
    private readonly Dictionary<(Guid agentId, Guid chatId), CancellationTokenSource> _jobs = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly AgentSignalManager _signalManager;

    public AgentScheduler(IServiceProvider serviceProvider, AgentSignalManager signalManager)
    {
        _serviceProvider = serviceProvider;
        _signalManager = signalManager;
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
                await AgentLoop(agentId, chatId, cts.Token);
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

    public async Task QueueTrigger(Trigger trigger)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();

        await dbContext.Triggers.AddAsync(trigger);
        await dbContext.SaveChangesAsync();

        Log.Information("[BACKGROUND] Queued trigger {TriggerId} of type {TriggerType} for agent {AgentId} and chat {ChatId}", trigger.Id, trigger.GetType().Name, trigger.AgentId, trigger.ChatId);

        // Signal the AgentLoop
        _signalManager.Signal(trigger.AgentId, trigger.ChatId);

        StartAgent(trigger.AgentId, trigger.ChatId);
    }

    private async Task AgentLoop(Guid agentId, Guid chatId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Wait for a signal
            _signalManager.WaitForSignal(agentId, chatId, ct);

            // get active wait conditions and triggers for this agent and chat
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AzureOpsCrewContext>();
            var waitConditions = await dbContext.WaitConditions
                .OfType<WaitCondition>()
                .Where(wc => wc.AgentId == agentId && wc.ChatId == chatId && wc.CompletedAt == null)
                .OrderBy(wc => wc.CreatedAt)
                .ToArrayAsync(cancellationToken: ct);
            var triggers = await dbContext.Triggers
                .OfType<Trigger>()
                .Where(t => t.AgentId == agentId && t.ChatId == chatId && t.CompletedAt == null)
                .OrderBy(t => t.CreatedAt)
                .ToArrayAsync(cancellationToken: ct);

            // try satisfy wait conditions
            var allWaitConditionsSatisfied = true;
            foreach (var waitCondition in waitConditions)
            {
                var satisfiedTrigger = triggers.FirstOrDefault(t => waitCondition.CanBeSatisfiedByTrigger(t));
                if (satisfiedTrigger != null)
                {
                    waitCondition.CompletedAt = DateTime.UtcNow;
                    waitCondition.SatisfiedByTriggerId = satisfiedTrigger.Id;
                    dbContext.WaitConditions.Update(waitCondition);
                    await dbContext.SaveChangesAsync(ct);
                    Log.Information("[BACKGROUND] Wait condition {WaitConditionId} for agent {AgentId} and chat {ChatId} satisfied by trigger {TriggerId}", waitCondition.Id, agentId, chatId, satisfiedTrigger.Id);
                }
                else
                {
                    allWaitConditionsSatisfied = false;
                }
            }
            if (waitConditions.Length == 0)
            {
                Log.Debug("[BACKGROUND] No wait conditions for agent {AgentId} and chat {ChatId}", agentId, chatId);
            }
            else if (allWaitConditionsSatisfied)
            {
                Log.Debug("[BACKGROUND] All wait conditions satisfied for agent {AgentId} and chat {ChatId}", agentId, chatId);
            }
            else
            {
                Log.Debug("[BACKGROUND] Not all wait conditions satisfied for agent {AgentId} and chat {ChatId}", agentId, chatId);
                continue;
            }

            // if there are no triggers, just continue the loop and wait for triggers to arrive
            if (triggers.Length == 0)
            {
                Log.Debug("[BACKGROUND] No triggers for agent {AgentId} and chat {ChatId}", agentId, chatId);
                continue;
            }

            // skip extra overlapping triggers
            var firstTrigger = triggers.First();
            foreach (var trigger in triggers)
            {
                if (trigger.Id != firstTrigger.Id)
                {
                    trigger.CompletedAt = DateTime.UtcNow;
                    trigger.IsSkipped = true;
                    dbContext.Triggers.Update(trigger);
                    await dbContext.SaveChangesAsync(ct);
                    Log.Warning("[BACKGROUND] Skipping overlapping trigger {TriggerId} for agent {AgentId} and chat {ChatId}", trigger.Id, agentId, chatId);
                }
            }

            // mark the first trigger as started
            firstTrigger.StartedAt = DateTime.UtcNow;
            dbContext.Triggers.Update(firstTrigger);
            await dbContext.SaveChangesAsync(ct);

            // run the agent for this trigger
            try
            {
                var agentRunService = scope.ServiceProvider.GetRequiredService<AgentRunService>();
                await agentRunService.Run(agentId, chatId, ct);
            }
            catch (Exception e)
            {
                Log.Error(e, "[BACKGROUND] Error running agent {AgentId} for chat {ChatId}", agentId, chatId);
            }

            // mark the first trigger as completed
            firstTrigger.CompletedAt = DateTime.UtcNow;
            dbContext.Triggers.Update(firstTrigger);
            await dbContext.SaveChangesAsync(ct);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("[BACKGROUND] Agent scheduler started");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken); // just keep the background service alive
        }
        Log.Information("[BACKGROUND] Agent scheduler stopping");
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
