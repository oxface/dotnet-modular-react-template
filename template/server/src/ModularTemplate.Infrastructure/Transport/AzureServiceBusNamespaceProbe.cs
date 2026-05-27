using Azure.Messaging.ServiceBus.Administration;

namespace ModularTemplate.Infrastructure.Transport;

public sealed class AzureServiceBusNamespaceProbe : IServiceBusNamespaceProbe
{
    public async Task ProbeAsync(string connectionString, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var adminClient = new ServiceBusAdministrationClient(connectionString);
        await adminClient.GetNamespacePropertiesAsync(cancellationToken);
    }
}
