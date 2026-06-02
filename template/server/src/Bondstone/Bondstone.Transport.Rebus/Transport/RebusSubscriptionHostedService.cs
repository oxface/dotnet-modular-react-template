using Microsoft.Extensions.Hosting;
using Rebus.ServiceProvider;

namespace Bondstone.Transport.Rebus;

internal sealed class RebusSubscriptionHostedService(
    IBusRegistry busRegistry,
    IEnumerable<ModuleEventSubscription> subscriptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (ModuleEventSubscription subscription in subscriptions)
        {
            await busRegistry
                .GetBus(MessagingBusKeys.ModuleQueue(subscription.ModuleName))
                .Subscribe(subscription.EventType);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
