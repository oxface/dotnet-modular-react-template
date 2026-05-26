namespace ModularTemplate.Outbox;

/// <summary>
/// Registry of local in-process event subscriptions used by the outbox dispatcher to fan
/// out integration events to the inbox of each subscribing module.
/// </summary>
public interface ILocalSubscriptionRegistry
{
    /// <summary>Returns the target module names that have subscribed to the given message type.</summary>
    IReadOnlyList<string> GetEventSubscribers(string messageType);

    /// <summary>Registers a module as a subscriber for the given stable message type name.</summary>
    void RegisterEventSubscriber(string messageType, string targetModule);
}
