using Bondstone.Internal;

namespace Bondstone.Messaging;

internal static class ModuleMessageHandlerRegistrationFactory
{
    public static IReadOnlyCollection<ModuleMessageHandlerRegistrationDescriptor> Create(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        Type[] messageTypes = handlerType.GetInterfaces()
            .Where(IsModuleMessageHandlerInterface)
            .Select(handlerInterface => handlerInterface.GenericTypeArguments[0])
            .Distinct()
            .ToArray();

        var descriptors = new List<ModuleMessageHandlerRegistrationDescriptor>(messageTypes.Length);

        foreach (Type messageType in messageTypes)
        {
            if (!typeof(IMessage).IsAssignableFrom(messageType))
            {
                throw new InvalidOperationException(
                    $"Module message handler '{handlerType.FullName}' handles '{messageType.FullName}', which must implement {nameof(IMessage)}.");
            }

            string messageIdentity = GetMessageIdentity(handlerType, messageType);
            descriptors.Add(new ModuleMessageHandlerRegistrationDescriptor(
                handlerType,
                messageType,
                messageIdentity,
                GetHandlerIdentity(handlerType, messageType, messageIdentity)));
        }

        return descriptors;
    }

    private static bool IsModuleMessageHandlerInterface(Type interfaceType)
    {
        return interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == typeof(IModuleMessageHandler<>);
    }

    private static string GetMessageIdentity(Type handlerType, Type messageType)
    {
        try
        {
            return MessageIdentityMetadata.GetRequiredIdentity(messageType);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException(
                $"Module message handler '{handlerType.FullName}' handles '{messageType.FullName}', " +
                "which must declare a valid Bondstone message identity.",
                ex);
        }
    }

    private static string GetHandlerIdentity(
        Type handlerType,
        Type messageType,
        string messageIdentity)
    {
        IntegrationEventHandlerIdentityAttribute? handlerIdentity = handlerType
            .GetCustomAttributes(typeof(IntegrationEventHandlerIdentityAttribute), inherit: false)
            .OfType<IntegrationEventHandlerIdentityAttribute>()
            .SingleOrDefault();

        if (handlerIdentity is null)
        {
            return messageIdentity;
        }

        if (!typeof(IIntegrationEvent).IsAssignableFrom(messageType))
        {
            throw new InvalidOperationException(
                $"Module message handler '{handlerType.FullName}' declares {nameof(IntegrationEventHandlerIdentityAttribute)}, " +
                $"but '{messageType.FullName}' is not an integration event.");
        }

        return handlerIdentity.Name.TrimRequired(nameof(IntegrationEventHandlerIdentityAttribute.Name));
    }
}
