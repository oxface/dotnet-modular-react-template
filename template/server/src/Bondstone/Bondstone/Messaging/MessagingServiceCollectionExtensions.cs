using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bondstone.Internal;

namespace Bondstone.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessagingAssembly<TMarker>(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Assembly assembly = typeof(TMarker).Assembly;

        AddMessagingRegistrationSource(services, assembly);

        return services;
    }

    public static IServiceCollection AddDurableRequestPolling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<DurableRequestOptions>();
        services.TryAddScoped<IDurableRequestSender, DurableRequestSender>();

        return services;
    }

    public static IServiceCollection AddModuleMessaging(
        this IServiceCollection services,
        string moduleName,
        params Type[] assemblyMarkers)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblyMarkers);

        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));

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
            IReadOnlyCollection<ModuleMessageHandlerRegistrationDescriptor> descriptors =
                ModuleMessageHandlerRegistrationFactory.Create(handlerType);

            if (descriptors.Count == 0)
            {
                continue;
            }

            services.TryAddScoped(handlerType);

            foreach (ModuleMessageHandlerRegistrationDescriptor descriptor in descriptors)
            {
                AddModuleMessageHandlerRegistration(services, moduleName, descriptor);
                RegisterTransportHandlerAdapters(services, descriptor.MessageType);

                if (typeof(IIntegrationEvent).IsAssignableFrom(descriptor.MessageType))
                {
                    AddModuleEventSubscription(services, moduleName, descriptor.MessageType);
                }
            }
        }
    }

    private static void AddModuleMessageHandlerRegistration(
        IServiceCollection services,
        string moduleName,
        ModuleMessageHandlerRegistrationDescriptor descriptor)
    {
        ModuleMessageHandlerRegistration[] existingRegistrations = services
            .Select(service => service.ImplementationInstance)
            .OfType<ModuleMessageHandlerRegistration>()
            .ToArray();

        if (!ModuleMessageHandlerRegistrationPolicy.ShouldRegister(moduleName, descriptor, existingRegistrations))
        {
            return;
        }

        services.AddSingleton(new ModuleMessageHandlerRegistration(
            moduleName,
            descriptor.MessageType,
            descriptor.HandlerType,
            descriptor.MessageIdentity,
            descriptor.HandlerIdentity));
    }

    private static void RegisterTransportHandlerAdapters(IServiceCollection services, Type messageType)
    {
        foreach (IModuleMessageTransportAdapter adapter in services
            .Select(service => service.ImplementationInstance)
            .OfType<IModuleMessageTransportAdapter>()
            .ToArray())
        {
            adapter.RegisterHandlerAdapter(services, messageType);
        }
    }
}
