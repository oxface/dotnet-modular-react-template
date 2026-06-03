using Bondstone.Internal;

namespace Bondstone.Messaging;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class DurableCommandIdentityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class IntegrationEventIdentityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IntegrationEventHandlerIdentityAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

internal static class MessageIdentityMetadata
{
    public static bool HasIdentity(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        return (typeof(IDurableCommand).IsAssignableFrom(messageType)
                && messageType.GetCustomAttributes(typeof(DurableCommandIdentityAttribute), inherit: false).Any())
            || (typeof(IIntegrationEvent).IsAssignableFrom(messageType)
                && messageType.GetCustomAttributes(typeof(IntegrationEventIdentityAttribute), inherit: false).Any());
    }

    public static string GetRequiredIdentity(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);

        bool isDurableCommand = typeof(IDurableCommand).IsAssignableFrom(messageType);
        bool isIntegrationEvent = typeof(IIntegrationEvent).IsAssignableFrom(messageType);
        DurableCommandIdentityAttribute? commandIdentity = messageType
            .GetCustomAttributes(typeof(DurableCommandIdentityAttribute), inherit: false)
            .OfType<DurableCommandIdentityAttribute>()
            .SingleOrDefault();
        IntegrationEventIdentityAttribute? eventIdentity = messageType
            .GetCustomAttributes(typeof(IntegrationEventIdentityAttribute), inherit: false)
            .OfType<IntegrationEventIdentityAttribute>()
            .SingleOrDefault();

        if (isDurableCommand && isIntegrationEvent)
        {
            throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' must not implement both {nameof(IDurableCommand)} and {nameof(IIntegrationEvent)}.");
        }

        if (isDurableCommand)
        {
            if (eventIdentity is not null)
            {
                throw new InvalidOperationException(
                    $"Durable command '{messageType.FullName}' must use {nameof(DurableCommandIdentityAttribute)}, not {nameof(IntegrationEventIdentityAttribute)}.");
            }

            return commandIdentity?.Name.TrimRequired(nameof(DurableCommandIdentityAttribute.Name))
                ?? throw new InvalidOperationException(
                    $"Durable command '{messageType.FullName}' must declare {nameof(DurableCommandIdentityAttribute)}.");
        }

        if (isIntegrationEvent)
        {
            if (commandIdentity is not null)
            {
                throw new InvalidOperationException(
                    $"Integration event '{messageType.FullName}' must use {nameof(IntegrationEventIdentityAttribute)}, not {nameof(DurableCommandIdentityAttribute)}.");
            }

            return eventIdentity?.Name.TrimRequired(nameof(IntegrationEventIdentityAttribute.Name))
                ?? throw new InvalidOperationException(
                    $"Integration event '{messageType.FullName}' must declare {nameof(IntegrationEventIdentityAttribute)}.");
        }

        throw new InvalidOperationException(
            $"Message type '{messageType.FullName}' must implement {nameof(IDurableCommand)} or {nameof(IIntegrationEvent)}.");
    }
}
