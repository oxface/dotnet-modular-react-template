using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModularTemplate.SharedKernel.Extensions;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.Handlers;

namespace ModularTemplate.Infrastructure.Transport;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessagingAssembly<TMarker>(this IServiceCollection services)
    {
        Assembly assembly = typeof(TMarker).Assembly;

        AddMessagingRegistrationSource(services, assembly);

        return services;
    }

    public static IServiceCollection AddModuleMessaging(
        this IServiceCollection services,
        string moduleName,
        params Type[] assemblyMarkers)
    {
        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        ArgumentNullException.ThrowIfNull(assemblyMarkers);

        if (assemblyMarkers.Any(marker => marker is null))
        {
            throw new ArgumentException(
                "Messaging assembly marker types must not contain null values.",
                nameof(assemblyMarkers));
        }

        foreach (Assembly assembly in assemblyMarkers.Select(marker => marker.Assembly).Distinct())
        {
            AddMessagingRegistrationSource(services, assembly);
            RegisterModuleMessageHandlers(services, normalizedModuleName, assembly);
        }

        return services;
    }

    public static IServiceCollection AddModuleEventSubscriptions(
        this IServiceCollection services,
        string moduleName,
        params Type[] eventTypes)
    {
        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        ArgumentNullException.ThrowIfNull(eventTypes);

        foreach (Type eventType in eventTypes)
        {
            if (!typeof(IIntegrationEvent).IsAssignableFrom(eventType))
            {
                throw new ArgumentException(
                    $"Subscription type '{eventType.FullName}' must implement {nameof(IIntegrationEvent)}.",
                    nameof(eventTypes));
            }

            AddModuleEventSubscription(services, normalizedModuleName, eventType);
        }

        return services;
    }

    private static void AddMessagingRegistrationSource(IServiceCollection services, Assembly assembly)
    {
        if (services.Any(service =>
                service.ServiceType == typeof(MessagingRegistrationSource)
                && service.ImplementationInstance is MessagingRegistrationSource source
                && source.Assembly == assembly))
        {
            return;
        }

        services.AddSingleton(new MessagingRegistrationSource(assembly));
    }

    private static void AddModuleEventSubscription(
        IServiceCollection services,
        string moduleName,
        Type eventType)
    {
        if (services.Any(service =>
                service.ServiceType == typeof(ModuleEventSubscription)
                && service.ImplementationInstance is ModuleEventSubscription subscription
                && string.Equals(subscription.ModuleName, moduleName, StringComparison.Ordinal)
                && subscription.EventType == eventType))
        {
            return;
        }

        services.AddSingleton(new ModuleEventSubscription(moduleName, eventType));
    }

    private static void RegisterModuleMessageHandlers(
        IServiceCollection services,
        string moduleName,
        Assembly assembly)
    {
        foreach (Type handlerType in assembly.DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false })
            .Select(type => type.AsType()))
        {
            Type[] messageTypes = handlerType.GetInterfaces()
                .Where(IsModuleMessageHandlerInterface)
                .Select(handlerInterface => handlerInterface.GenericTypeArguments[0])
                .Distinct()
                .ToArray();

            if (messageTypes.Length == 0)
            {
                continue;
            }

            services.TryAddScoped(handlerType);

            foreach (Type messageType in messageTypes)
            {
                if (!typeof(IMessage).IsAssignableFrom(messageType))
                {
                    throw new InvalidOperationException(
                        $"Module message handler '{handlerType.FullName}' handles '{messageType.FullName}', which must implement {nameof(IMessage)}.");
                }

                string messageIdentity = GetMessageIdentity(handlerType, messageType);

                AddModuleMessageHandlerRegistration(
                    services,
                    moduleName,
                    messageType,
                    handlerType,
                    messageIdentity);

                services.TryAddEnumerable(ServiceDescriptor.Scoped(
                    typeof(IHandleMessages<>).MakeGenericType(messageType),
                    typeof(ModuleScopedRebusHandler<>).MakeGenericType(messageType)));
            }
        }
    }

    private static bool IsModuleMessageHandlerInterface(Type interfaceType)
    {
        return interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == typeof(IModuleMessageHandler<>);
    }

    private static void AddModuleMessageHandlerRegistration(
        IServiceCollection services,
        string moduleName,
        Type messageType,
        Type handlerType,
        string messageIdentity)
    {
        if (services.Any(service =>
                service.ServiceType == typeof(ModuleMessageHandlerRegistration)
                && service.ImplementationInstance is ModuleMessageHandlerRegistration registration
                && string.Equals(registration.ModuleName, moduleName, StringComparison.Ordinal)
                && registration.MessageType == messageType))
        {
            if (services.Any(service =>
                    service.ServiceType == typeof(ModuleMessageHandlerRegistration)
                    && service.ImplementationInstance is ModuleMessageHandlerRegistration registration
                    && string.Equals(registration.ModuleName, moduleName, StringComparison.Ordinal)
                    && registration.MessageType == messageType
                    && registration.HandlerType != handlerType))
            {
                throw new InvalidOperationException(
                    $"Module '{moduleName}' already has a message handler for '{messageType.FullName}'. " +
                    "Use one module message handler per message identity and fan out to module-local services when needed.");
            }

            return;
        }

        services.AddSingleton(new ModuleMessageHandlerRegistration(
            moduleName,
            messageType,
            handlerType,
            messageIdentity));
    }

    private static string GetMessageIdentity(Type handlerType, Type messageType)
    {
        return messageType
            .GetCustomAttributes(typeof(MessageIdentityAttribute), inherit: false)
            .OfType<MessageIdentityAttribute>()
            .SingleOrDefault()
            ?.Name.TrimRequired(nameof(MessageIdentityAttribute.Name))
            ?? throw new InvalidOperationException(
                $"Module message handler '{handlerType.FullName}' handles '{messageType.FullName}', " +
                $"which must declare {nameof(MessageIdentityAttribute)}.");
    }
}
