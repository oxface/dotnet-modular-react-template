namespace Bondstone.Transport.Rebus;

public sealed class RebusTransportOptions
{
    internal RebusInternalTransport InternalTransport { get; set; }

    public string QueuePrefix { get; set; } = "modular-template";

    public RebusPostgresTransportOptions Postgres { get; set; } = new();

    public RebusAzureServiceBusTransportOptions AzureServiceBus { get; set; } = new();
}

public sealed class RebusPostgresTransportOptions
{
    public string ConnectionStringName { get; set; } = "modular-template-host";

    public string TransportSchema { get; set; } = "transport";

    public string TransportTable { get; set; } = "rebus_messages";

    public string SubscriptionTable { get; set; } = "rebus_subscriptions";

    public bool AutoCreateSubscriptionTable { get; set; } = true;
}

public sealed class RebusAzureServiceBusTransportOptions
{
    public string ConnectionStringName { get; set; } = "messaging-service-bus";
}
