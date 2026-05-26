namespace ModularTemplate.Outbox;

public sealed class LocalSubscriptionRegistry : ILocalSubscriptionRegistry
{
    private readonly Dictionary<string, List<string>> _eventSubscribers = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public IReadOnlyList<string> GetEventSubscribers(string messageType)
    {
        lock (_sync)
        {
            return _eventSubscribers.TryGetValue(messageType, out List<string>? subscribers)
                ? [.. subscribers]
                : [];
        }
    }

    public void RegisterEventSubscriber(string messageType, string targetModule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetModule);

        lock (_sync)
        {
            if (!_eventSubscribers.TryGetValue(messageType, out List<string>? subscribers))
            {
                subscribers = [];
                _eventSubscribers[messageType] = subscribers;
            }

            if (!subscribers.Contains(targetModule, StringComparer.Ordinal))
            {
                subscribers.Add(targetModule);
            }
        }
    }
}
