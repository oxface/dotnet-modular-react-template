using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.SharedKernel.Extensions;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.PostgreSql;
using Rebus.Config;
using Rebus.Transport.InMem;

namespace ModularTemplate.Infrastructure.Transport;

public static class TransportConfiguration
{
    private static readonly InMemNetwork InMemNetwork = new();

    public static TBuilder AddTransport<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        DurableMessagingOptions messagingOptions = builder.Configuration
            .GetSection("Messaging")
            .Get<DurableMessagingOptions>()
            ?? new DurableMessagingOptions();

        string[] modules = messagingOptions.Modules.TrimDistinctRequired(nameof(messagingOptions.Modules));

        builder.Services.AddSingleton<IMessageTypeRegistry>(sp =>
            MessageTypeRegistryFactory.Create(sp.GetServices<MessagingRegistrationSource>()));
        builder.Services.AddScoped<IModuleUnitOfWorkResolver, ModuleUnitOfWorkResolver>();
        builder.Services.AddScoped<IDurableCommandSender, DurableCommandSender>();
        builder.Services.AddScoped<IRetryDelayPolicy, ConfiguredRetryDelayPolicy>();
        builder.Services.AddScoped<IOutboxRouteResolver, OutboxRouteResolver>();
        builder.Services.AddScoped<IOutboxTransport, RebusOutboxTransport>();
        builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        builder.Services.AddHostedService<RebusSubscriptionHostedService>();
        builder.Services.AddHostedService<OutboxDispatcherBackgroundService>();
        builder.Services.AddOptions<DurableMessagingOptions>()
            .Bind(builder.Configuration.GetSection("Messaging"))
            .Validate(options => options.Modules.Any(module => !string.IsNullOrWhiteSpace(module)),
                "Messaging:Modules must contain at least one module name.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.QueuePrefix),
                "Messaging:QueuePrefix is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionStringName),
                "Messaging:ConnectionStringName is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TransportSchema),
                "Messaging:TransportSchema is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.TransportTable),
                "Messaging:TransportTable is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.SubscriptionTable),
                "Messaging:SubscriptionTable is required.")
            .Validate(options => options.PollingInterval > TimeSpan.Zero,
                "Messaging:PollingInterval must be greater than zero.")
            .Validate(options => options.BatchSize > 0,
                "Messaging:BatchSize must be greater than zero.")
            .Validate(options => options.MaxAttempts > 0,
                "Messaging:MaxAttempts must be greater than zero.")
            .Validate(options => options.LockTimeout > TimeSpan.Zero,
                "Messaging:LockTimeout must be greater than zero.")
            .ValidateOnStart();

        for (int index = 0; index < modules.Length; index++)
        {
            string moduleName = modules[index];
            bool isDefaultBus = index == 0;

            builder.Services.AddRebus(
                configure => ConfigureInternalTransport(
                    configure,
                    builder.Configuration,
                    messagingOptions,
                    moduleName),
                isDefaultBus: isDefaultBus,
                key: MessagingBusKeys.Internal(moduleName));
        }

        return builder;
    }

    private static RebusConfigurer ConfigureInternalTransport(
        RebusConfigurer configure,
        IConfiguration configuration,
        DurableMessagingOptions options,
        string moduleName)
    {
        string queueName = $"{options.QueuePrefix}.{moduleName}";
        if (options.Transport == DurableMessagingTransport.InMemory)
        {
            configure.Transport(t => t.UseInMemoryTransport(InMemNetwork, queueName));
            return configure;
        }

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
