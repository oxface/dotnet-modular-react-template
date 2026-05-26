namespace ModularTemplate.Outbox;

public sealed class DurableMessagingOptions
{
    public bool Enabled { get; set; } = true;

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(2);

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 5;

    public TimeSpan LockTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
