using AzureOpsCrew.Domain.Triggers;
using Serilog;

namespace AzureOpsCrew.Api.Background.Triggers;

public class ScheduledTriggerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    public ScheduledTriggerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("[TRIGGERS] ScheduledTriggerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var evaluator = scope.ServiceProvider.GetRequiredService<TriggerEvaluator>();

                var context = new TriggerContext { UtcNow = DateTime.UtcNow };
                await evaluator.EvaluateAsync(context, [TriggerType.Scheduled], stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "[TRIGGERS] Error evaluating scheduled triggers");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
