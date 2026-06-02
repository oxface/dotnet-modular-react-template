using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Bondstone.EntityFrameworkCore.Persistence;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Outbox;

public sealed class OutboxDispatcherBackgroundService<TDbContext>(
    IServiceProvider serviceProvider,
    IOptions<DurableMessagingOptions> options,
    ILogger<OutboxDispatcherBackgroundService<TDbContext>> logger) : BackgroundService
    where TDbContext : DbContext, IModuleDbContext
{
    private readonly DurableMessagingOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using IServiceScope scope = serviceProvider.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcher<TDbContext>>();
                await dispatcher.DispatchPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox dispatcher background iteration failed for {DbContextType}.", typeof(TDbContext).FullName);
            }

            await Task.Delay(_options.PollingInterval, stoppingToken);
        }
    }
}
