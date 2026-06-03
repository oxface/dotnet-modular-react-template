namespace Bondstone.Messaging;

public sealed class DurableRequestOptions
{
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromMilliseconds(250);
}
