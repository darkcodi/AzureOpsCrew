using System.Threading.Channels;
using AzureOpsCrew.Domain.Triggers;
using Serilog;

namespace AzureOpsCrew.Api.Background.Triggers;

public class TriggerEventBus : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Channel<TriggerContext> _channel = Channel.CreateUnbounded<TriggerContext>();

    public TriggerEventBus(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task PublishAsync(TriggerContext context)
    {
        await _channel.Writer.WriteAsync(context);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("[TRIGGERS] TriggerEventBus started");

        await foreach (var context in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = _serviceProvider.CreateAsyncScope();
                var evaluator = scope.ServiceProvider.GetRequiredService<TriggerEvaluator>();

                await evaluator.EvaluateAsync(context, [TriggerType.Event], stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Log.Error(ex, "[TRIGGERS] Error evaluating event trigger");
            }
        }
    }
}
