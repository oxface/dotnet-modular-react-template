namespace Bondstone.Messaging;

public sealed class DurableMessagingOptions
{
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
