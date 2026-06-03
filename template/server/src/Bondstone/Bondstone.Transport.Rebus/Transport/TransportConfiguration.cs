using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Bondstone.Internal;
using Bondstone.Messaging;
using Rebus.PostgreSql;
using Rebus.Config;
using Rebus.Pipeline;
using Rebus.Pipeline.Receive;
using Rebus.Serialization.Custom;
using Rebus.ServiceProvider;

namespace Bondstone.Transport.Rebus;

public static class TransportConfiguration
{
    public static TBuilder AddRebusTransport<TBuilder>(
        this TBuilder builder,
        Action<RebusTransportBuilder>? configureTransport = null)
        where TBuilder : IHostApplicationBuilder
    {
        DurableMessagingOptions messagingOptions = builder.Configuration
            .GetSection("Messaging")
            .Get<DurableMessagingOptions>()
            ?? new DurableMessagingOptions();
        var transportBuilder = new RebusTransportBuilder();
        configureTransport?.Invoke(transportBuilder);

        if (transportBuilder.InternalTransport == RebusInternalTransport.None)
        {
            throw new InvalidOperationException(
                "Rebus internal transport is not configured. Call UsePostgresInternalTransport, UseAzureServiceBusInternalTransport, or another internal transport extension.");
        }

        builder.Services.AddSingleton<IMessageTypeRegistry>(sp =>
            MessageTypeRegistryFactory.Create(sp.GetServices<MessagingRegistrationSource>()));
        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IModuleMessageTransportAdapter>(
                new RebusModuleMessageTransportAdapter()));
        builder.Services.AddScoped<IOutboxRouteResolver, OutboxRouteResolver>();
        builder.Services.AddSingleton<IValidateOptions<DurableMessagingOptions>, DurableMessagingOptionsValidator>();
        builder.Services.AddSingleton<IValidateOptions<RebusTransportOptions>, RebusTransportOptionsValidator>();
        builder.Services.AddScoped<IOutboxTransport, RebusOutboxTransport>();
        RegisterExistingModuleMessageHandlerAdapters(builder.Services);
        if (transportBuilder.InternalTransport == RebusInternalTransport.Postgres)
        {
            builder.Services.TryAddSingleton<RebusPostgresSchemaInitializer>();
        }

        builder.Services.AddOptions<DurableMessagingOptions>()
            .Bind(builder.Configuration.GetSection("Messaging"))
            .ValidateOnStart();
        builder.Services.AddOptions<RebusTransportOptions>()
            .Configure(options => CopyTransportOptions(transportBuilder.Options, options))
            .ValidateOnStart();

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
                    transportBuilder.InternalTransport,
                    transportBuilder.Options,
                    moduleName),
                isDefaultBus: isDefaultBus,
                key: MessagingBusKeys.ModuleQueue(moduleName));
            builder.Services.AddSingleton<IHostedService>(serviceProvider =>
                new RebusSubscriptionHostedService(
                    moduleName,
                    serviceProvider.GetRequiredService<IBusRegistry>(),
                    serviceProvider.GetServices<ModuleEventSubscription>()));
        }

        return builder;
    }

    private static void RegisterExistingModuleMessageHandlerAdapters(IServiceCollection services)
    {
        var adapter = new RebusModuleMessageTransportAdapter();
        foreach (Type messageType in services
            .Select(service => service.ImplementationInstance)
            .OfType<ModuleMessageHandlerRegistration>()
            .Select(registration => registration.MessageType)
            .Distinct())
        {
            adapter.RegisterHandlerAdapter(services, messageType);
        }
    }

    private static RebusConfigurer ConfigureInternalTransport(
        RebusConfigurer configure,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        RebusInternalTransport internalTransport,
        RebusTransportOptions options,
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

        return internalTransport switch
        {
            RebusInternalTransport.Postgres => ConfigurePostgresInternalTransport(
                configure,
                configuration,
                options,
                queueName),
            RebusInternalTransport.AzureServiceBus => ConfigureAzureServiceBusInternalTransport(
                configure,
                configuration,
                options,
                queueName),
            _ => throw new InvalidOperationException(
                $"Unsupported Rebus internal transport '{internalTransport}'.")
        };
    }

    private static RebusConfigurer ConfigureAzureServiceBusInternalTransport(
        RebusConfigurer configure,
        IConfiguration configuration,
        RebusTransportOptions options,
        string queueName)
    {
        string connectionString = configuration.GetConnectionString(options.AzureServiceBus.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{options.AzureServiceBus.ConnectionStringName}' is required for Rebus Azure Service Bus transport.");

        configure.Transport(t => t.UseAzureServiceBus(connectionString, queueName));
        return configure;
    }

    private static RebusConfigurer ConfigurePostgresInternalTransport(
        RebusConfigurer configure,
        IConfiguration configuration,
        RebusTransportOptions options,
        string queueName)
    {
        string connectionString = configuration.GetConnectionString(options.Postgres.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{options.Postgres.ConnectionStringName}' is required for Rebus PostgreSQL transport.");

        configure.Transport(t => t.UsePostgreSql(
            connectionString,
            options.Postgres.TransportTable,
            queueName,
            expiredMessagesCleanupInterval: null,
            schemaName: options.Postgres.TransportSchema));
        var connectionProvider = new PostgresConnectionHelper(connectionString);

        configure.Subscriptions(s => s.StoreInPostgres(
            connectionProvider,
            options.Postgres.SubscriptionTable,
            isCentralized: true,
            automaticallyCreateTables: options.Postgres.AutoCreateSubscriptionTable,
            schemaName: options.Postgres.TransportSchema));
        return configure;
    }

    private static void CopyTransportOptions(
        RebusTransportOptions source,
        RebusTransportOptions target)
    {
        target.InternalTransport = source.InternalTransport;
        target.QueuePrefix = source.QueuePrefix;
        target.Postgres = source.Postgres;
        target.AzureServiceBus = source.AzureServiceBus;
    }
}
