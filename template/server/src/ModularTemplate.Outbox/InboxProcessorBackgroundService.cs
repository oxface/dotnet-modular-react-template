using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ModularTemplate.Outbox;

public sealed class InboxProcessorBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<DurableMessagingOptions> options,
    ILogger<InboxProcessorBackgroundService> logger) : BackgroundService
{
    private readonly DurableMessagingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IInboxProcessor>();
                await processor.ProcessPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Inbox processor background iteration failed.");
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }
}
