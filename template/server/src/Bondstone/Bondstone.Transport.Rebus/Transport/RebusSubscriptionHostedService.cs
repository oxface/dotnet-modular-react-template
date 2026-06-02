using Microsoft.Extensions.Hosting;
using Rebus.ServiceProvider;

namespace Bondstone.Transport.Rebus;

internal sealed class RebusSubscriptionHostedService(
    string moduleName,
    IBusRegistry busRegistry,
    IEnumerable<ModuleEventSubscription> subscriptions) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (ModuleEventSubscription subscription in subscriptions.Where(subscription =>
            string.Equals(subscription.ModuleName, moduleName, StringComparison.Ordinal)))
        {
            await busRegistry
                .GetBus(MessagingBusKeys.ModuleQueue(moduleName))
                .Subscribe(subscription.EventType);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
