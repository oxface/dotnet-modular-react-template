using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        services.TryAddScoped<IInboxMessageProcessor, InboxMessageProcessor>();
        services.TryAddScoped<IRetryDelayPolicy, ConfiguredRetryDelayPolicy>();
        services.TryAddScoped<IOutboxDispatcher, OutboxDispatcher>();
        services.TryAddScoped<IStoredDomainEventMapper, StoredDomainEventMapper>();
        services.TryAddScoped<IModuleBoundary, EntityFrameworkCoreModuleBoundary>();
        services.TryAddScoped<IModuleUnitOfWorkContext, ModuleUnitOfWorkContext>();
        services.TryAddScoped<IModulePersistenceResolver, ModulePersistenceResolver>();
        services.TryAddScoped<IModuleUnitOfWorkResolver, ModuleUnitOfWorkResolver>();
        services.TryAddScoped<IModuleCommandBoundaryResolver, ModuleUnitOfWorkResolver>();
        services.TryAddScoped<ModuleUnitOfWork<TDbContext>>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            Microsoft.Extensions.Options.IValidateOptions<DurableMessagingOptions>,
            EntityFrameworkCoreDurableMessagingOptionsValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<
            Microsoft.Extensions.Hosting.IHostedService,
            OutboxDispatcherBackgroundService>());
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
