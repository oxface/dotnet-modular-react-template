namespace Bondstone.Transport.Rebus;

public sealed class RebusTransportOptions
{
    public string QueuePrefix { get; set; } = "modular-template";

    public RebusPostgresTransportOptions Postgres { get; set; } = new();
}

public sealed class RebusPostgresTransportOptions
{
    public string ConnectionStringName { get; set; } = "modular-template-host";

    public string TransportSchema { get; set; } = "transport";

    public string TransportTable { get; set; } = "rebus_messages";

    public string SubscriptionTable { get; set; } = "rebus_subscriptions";

    public bool AutoCreateSchema { get; set; } = true;
}
