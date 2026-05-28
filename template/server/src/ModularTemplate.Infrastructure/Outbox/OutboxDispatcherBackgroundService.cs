using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModularTemplate.Infrastructure.Outbox;

public sealed class OutboxDispatcherBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<DurableMessagingOptions> options,
    ILogger<OutboxDispatcherBackgroundService> logger) : BackgroundService
{
    private readonly DurableMessagingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Outbox dispatcher is disabled by configuration.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceProvider.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatcher background iteration failed.");
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }
}
