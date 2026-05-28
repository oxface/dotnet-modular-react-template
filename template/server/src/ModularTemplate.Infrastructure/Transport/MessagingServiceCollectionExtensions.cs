using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModularTemplate.SharedKernel.Extensions;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.Config;

namespace ModularTemplate.Infrastructure.Transport;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddMessagingAssembly<TMarker>(this IServiceCollection services)
    {
        Assembly assembly = typeof(TMarker).Assembly;

        services.AutoRegisterHandlersFromAssembly(assembly, static _ => true);
        services.AddSingleton(new MessagingRegistrationSource(assembly));

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

            services.AddSingleton(new ModuleEventSubscription(normalizedModuleName, eventType));
        }

        return services;
    }
}
