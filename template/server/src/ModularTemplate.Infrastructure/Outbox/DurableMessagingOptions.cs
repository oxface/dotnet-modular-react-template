namespace ModularTemplate.Infrastructure.Outbox;

public enum DurableMessagingTransport
{
    Postgres = 1,
    InMemory = 2
}

public sealed class DurableMessagingOptions
{
    public bool Enabled { get; set; } = true;

    public DurableMessagingTransport Transport { get; set; } = DurableMessagingTransport.Postgres;

    public string ConnectionStringName { get; set; } = "modular-template-host";

    public string QueuePrefix { get; set; } = "modular-template";

    public string TransportSchema { get; set; } = "transport";

    public string TransportTable { get; set; } = "rebus_messages";

    public string SubscriptionTable { get; set; } = "rebus_subscriptions";

    public List<string> Modules { get; set; } = ["identity", "operations"];

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 5;

    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(2);

    public List<TimeSpan> RetryDelays { get; set; } =
    [
        TimeSpan.Zero,
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15)
    ];
}
