using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModularTemplate.Infrastructure.Outbox;
using Rebus.ServiceProvider;

namespace ModularTemplate.Infrastructure.Transport;

internal sealed class RebusSubscriptionHostedService(
    IBusRegistry busRegistry,
    IEnumerable<ModuleEventSubscription> subscriptions,
    IOptions<DurableMessagingOptions> options) : IHostedService
{
    private readonly DurableMessagingOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        foreach (ModuleEventSubscription subscription in subscriptions)
        {
            await busRegistry
                .GetBus(MessagingBusKeys.Internal(subscription.ModuleName))
                .Subscribe(subscription.EventType);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
