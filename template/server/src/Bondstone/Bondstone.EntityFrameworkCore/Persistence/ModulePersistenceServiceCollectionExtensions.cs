using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Bondstone.Commands;
using Bondstone.EntityFrameworkCore.Outbox;
using Bondstone.EntityFrameworkCore.Inbox;
using Bondstone.EntityFrameworkCore.Persistence.DomainEvents;
using Bondstone.Internal;
using Bondstone;
using Bondstone.Messaging;

namespace Bondstone.EntityFrameworkCore.Persistence;

public static class ModulePersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddEntityFrameworkCoreModulePersistence<TDbContext>(
        this IServiceCollection services,
        string moduleName,
        params Type[] commandTypes)
        where TDbContext : DbContext, IModuleDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(commandTypes);

        string normalizedModuleName = moduleName.TrimRequired(nameof(moduleName));
        if (commandTypes.Any(commandType => commandType is null))
        {
            throw new ArgumentException(
                "Command types must not contain null values.",
                nameof(commandTypes));
        }

        services.TryAddScoped<OutboxWriter<TDbContext>>();
        services.TryAddScoped<IDurableCommandSender, DurableCommandSender>();
        services.TryAddScoped<IModuleMessageInbox, EntityFrameworkCoreModuleMessageInbox>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IModuleMessageInboxExecutor,
            EntityFrameworkCoreModuleMessageInbox<TDbContext>>());
        services.TryAddScoped<IInboxMessageProcessor, InboxMessageProcessor>();
        services.TryAddScoped<IRetryDelayPolicy, ConfiguredRetryDelayPolicy>();
        services.TryAddScoped<OutboxDispatcher<TDbContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxDispatcher, OutboxDispatcher<TDbContext>>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOutboxMaintenance, OutboxMaintenance<TDbContext>>());
        services.TryAddScoped<IStoredDomainEventMapper, StoredDomainEventMapper>();
        services.TryAddScoped<IModuleBoundary, EntityFrameworkCoreModuleBoundary>();
        services.TryAddScoped<IEntityFrameworkCoreModuleMigrator, EntityFrameworkCoreModuleMigrator>();
        services.TryAddScoped<EntityFrameworkCoreModuleBoundary<TDbContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<
            IModuleBoundaryExecutor,
            EntityFrameworkCoreModuleBoundary<TDbContext>>());
        services.TryAddScoped<IModuleUnitOfWorkContext, ModuleUnitOfWorkContext>();
        services.TryAddScoped<IModulePersistenceResolver, ModulePersistenceResolver>();
        services.TryAddScoped<IModuleUnitOfWorkResolver, ModuleUnitOfWorkResolver>();
        services.TryAddScoped<IModuleCommandBoundaryResolver, ModuleUnitOfWorkResolver>();
        services.TryAddScoped<ModuleUnitOfWork<TDbContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(IModuleCommandPipelineBehavior<,>),
            typeof(ModuleUnitOfWorkCommandBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            Microsoft.Extensions.Options.IValidateOptions<DurableMessagingOptions>,
            EntityFrameworkCoreDurableMessagingOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            Microsoft.Extensions.Options.IValidateOptions<DurableMessagingOptions>,
            EntityFrameworkCoreModuleTopologyValidator>());
        services.AddModuleTopology(normalizedModuleName);
        services.AddSingleton(new ModuleRuntimeRegistration(
            normalizedModuleName,
            typeof(TDbContext),
            typeof(EntityFrameworkCoreModuleBoundary<TDbContext>),
            typeof(EntityFrameworkCoreModuleMessageInbox<TDbContext>),
            typeof(OutboxDispatcher<TDbContext>),
            typeof(OutboxDispatcherBackgroundService<TDbContext>)));
        AddModulePersistenceRegistration(
            services,
            normalizedModuleName,
            typeof(TDbContext),
            commandTypes
                .Distinct()
                .ToArray(),
            serviceProvider => serviceProvider.GetRequiredService<TDbContext>(),
            serviceProvider => serviceProvider.GetRequiredService<ModuleUnitOfWork<TDbContext>>(),
            serviceProvider => serviceProvider.GetRequiredService<OutboxWriter<TDbContext>>());

        return services;
    }

    public static IServiceCollection AddEntityFrameworkCoreModuleOutboxDispatchers(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        Type[] workerTypes = services
            .Select(service => service.ImplementationInstance)
            .OfType<ModuleRuntimeRegistration>()
            .Select(registration => registration.OutboxWorkerType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();

        if (workerTypes.Length == 0)
        {
            throw new InvalidOperationException(
                "No module persistence runtimes are registered. Call module persistence registrations before adding outbox dispatchers.");
        }

        foreach (Type workerType in workerTypes)
        {
            if (!typeof(Microsoft.Extensions.Hosting.IHostedService).IsAssignableFrom(workerType))
            {
                throw new InvalidOperationException(
                    $"Module outbox worker type '{workerType.FullName}' must implement {typeof(Microsoft.Extensions.Hosting.IHostedService).FullName}.");
            }

            if (services.Any(service =>
                    service.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                    && service.ImplementationType == workerType))
            {
                continue;
            }

            services.Add(ServiceDescriptor.Singleton(
                typeof(Microsoft.Extensions.Hosting.IHostedService),
                workerType));
        }

        return services;
    }

    private static void AddModulePersistenceRegistration(
        IServiceCollection services,
        string moduleName,
        Type dbContextType,
        Type[] commandTypes,
        Func<IServiceProvider, IModuleDbContext> dbContextFactory,
        Func<IServiceProvider, IModuleUnitOfWork> unitOfWorkFactory,
        Func<IServiceProvider, IOutboxWriter> outboxWriterFactory)
    {
        services.AddSingleton(new ModulePersistenceRegistration(
            moduleName,
            dbContextType,
            commandTypes,
            dbContextFactory,
            unitOfWorkFactory,
            outboxWriterFactory));
    }
}
