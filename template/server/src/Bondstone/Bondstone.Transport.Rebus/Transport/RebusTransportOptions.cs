namespace Bondstone.Transport.Rebus;

public sealed class RebusTransportOptions
{
    internal RebusInternalTransport InternalTransport { get; set; }

    public string QueuePrefix { get; set; } = string.Empty;

    public RebusWorkerOptions Workers { get; set; } = new();

    public RebusPostgresTransportOptions Postgres { get; set; } = new();

    public RebusAzureServiceBusTransportOptions AzureServiceBus { get; set; } = new();
}

public sealed class RebusWorkerOptions
{
    public int NumberOfWorkers { get; set; } = 1;

    public int MaxParallelism { get; set; } = 1;

    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
}

public sealed class RebusPostgresTransportOptions
{
    public string ConnectionStringName { get; set; } = string.Empty;

    public string TransportSchema { get; set; } = "transport";

    public string TransportTable { get; set; } = "rebus_messages";

    public string SubscriptionTable { get; set; } = "rebus_subscriptions";

    public bool AutoCreateSubscriptionTable { get; set; } = true;
}

public sealed class RebusAzureServiceBusTransportOptions
{
    public string ConnectionStringName { get; set; } = string.Empty;
}
