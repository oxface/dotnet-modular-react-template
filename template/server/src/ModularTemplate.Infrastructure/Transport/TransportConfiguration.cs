using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModularTemplate.Infrastructure.Inbox;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.SharedKernel.Extensions;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.PostgreSql;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Serialization.Custom;

namespace ModularTemplate.Infrastructure.Transport;

public static class TransportConfiguration
{
    public static TBuilder AddTransport<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        DurableMessagingOptions messagingOptions = builder.Configuration
            .GetSection("Messaging")
            .Get<DurableMessagingOptions>()
            ?? new DurableMessagingOptions();

        builder.Services.AddSingleton<IMessageTypeRegistry>(sp =>
            MessageTypeRegistryFactory.Create(sp.GetServices<MessagingRegistrationSource>()));
        builder.Services.TryAddScoped<IModuleUnitOfWorkContext, ModuleUnitOfWorkContext>();
        builder.Services.TryAddScoped<IModulePersistenceResolver, ModulePersistenceResolver>();
        builder.Services.AddScoped<IModuleUnitOfWorkResolver, ModuleUnitOfWorkResolver>();
        builder.Services.AddScoped<IDurableCommandSender, DurableCommandSender>();
        builder.Services.AddScoped<IInboxMessageProcessor, InboxMessageProcessor>();
        builder.Services.AddScoped<IRetryDelayPolicy, ConfiguredRetryDelayPolicy>();
        builder.Services.AddScoped<IOutboxRouteResolver, OutboxRouteResolver>();
        builder.Services.AddSingleton<IValidateOptions<DurableMessagingOptions>, DurableMessagingOptionsValidator>();
        if (messagingOptions.Enabled)
        {
            builder.Services.AddScoped<IOutboxTransport, RebusOutboxTransport>();
            builder.Services.AddScoped<IOutboxDispatchLock, PostgresAdvisoryOutboxDispatchLock>();
            builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
            builder.Services.AddHostedService<TransportSchemaInitializerHostedService>();
            builder.Services.AddHostedService<RebusSubscriptionHostedService>();
            builder.Services.AddHostedService<OutboxDispatcherBackgroundService>();
        }
        builder.Services.AddOptions<DurableMessagingOptions>()
            .Bind(builder.Configuration.GetSection("Messaging"))
            .ValidateOnStart();

        if (!messagingOptions.Enabled)
        {
            return builder;
        }

        string[] modules;
        try
        {
            modules = messagingOptions.Modules.TrimDistinctRequired(nameof(messagingOptions.Modules));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return builder;
        }

        for (int index = 0; index < modules.Length; index++)
        {
            string moduleName = modules[index];
            bool isDefaultBus = index == 0;

            builder.Services.AddRebus(
                (configure, serviceProvider) => ConfigureInternalTransport(
                    configure,
                    serviceProvider,
                    builder.Configuration,
                    messagingOptions,
                    moduleName),
                isDefaultBus: isDefaultBus,
                key: MessagingBusKeys.ModuleQueue(moduleName));
        }

        return builder;
    }

    private static RebusConfigurer ConfigureInternalTransport(
        RebusConfigurer configure,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        DurableMessagingOptions options,
        string moduleName)
    {
        string queueName = $"{options.QueuePrefix}.{moduleName}";
        IMessageTypeRegistry messageTypeRegistry = serviceProvider.GetRequiredService<IMessageTypeRegistry>();

        configure.Serialization(serializer =>
        {
            var typeNames = serializer.UseCustomMessageTypeNames();

            foreach (MessageTypeRegistration registration in messageTypeRegistry.Registrations
                .OrderBy(registration => registration.MessageTypeName, StringComparer.Ordinal))
            {
                typeNames.AddWithCustomName(
                    registration.ClrType,
                    registration.MessageTypeName);
            }
        });

        configure.Options(o => o.Decorate<IPipeline>(context =>
        {
            var injector = new PipelineStepInjector(context.Get<IPipeline>());
            injector.OnReceive(
                new ReceivingModuleHeaderStep(moduleName),
                PipelineRelativePosition.Before,
                typeof(ActivateHandlersStep));
            return injector;
        }));

        if (options.Transport == DurableMessagingTransport.Postgres)
        {
            string connectionString = configuration.GetConnectionString(options.ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"Connection string '{options.ConnectionStringName}' is required when Messaging:Transport is Postgres.");

            configure.Transport(t => t.UsePostgreSql(
                connectionString,
                options.TransportTable,
                queueName,
                expiredMessagesCleanupInterval: null,
                schemaName: options.TransportSchema));
            var connectionProvider = new PostgresConnectionHelper(connectionString);

            configure.Subscriptions(s => s.StoreInPostgres(
                connectionProvider,
                options.SubscriptionTable,
                isCentralized: true,
                automaticallyCreateTables: true,
                schemaName: options.TransportSchema));
            return configure;
        }

        throw new InvalidOperationException($"Unsupported Messaging:Transport '{options.Transport}'.");
    }

}
