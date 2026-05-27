using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularTemplate.Infrastructure.Outbox;
using ModularTemplate.Infrastructure.Persistence;
using ModularTemplate.SharedKernel.Messaging;
using Rebus.AzureServiceBus;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;

namespace ModularTemplate.Infrastructure.Transport;

public static class TransportConfiguration
{
    private static readonly InMemNetwork InMemNetwork = new();

    public static TBuilder AddTransport<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSingleton<ILocalSubscriptionRegistry, LocalSubscriptionRegistry>();
        builder.Services.AddSingleton<IMessageTypeRegistry, MessageTypeRegistry>();
        builder.Services.AddScoped<IUnitOfWork, ModuleUnitOfWork>();
        builder.Services.AddOptions<DurableMessagingOptions>()
            .Bind(builder.Configuration.GetSection("Messaging"));

        string transport = MessagingTransportConfiguration.ResolveTransport(
            builder.Configuration,
            builder.Environment.EnvironmentName);
        string? serviceBusConnectionString = builder.Configuration["ConnectionStrings:service-bus"];

        builder.Services.AddSingleton<IServiceBusNamespaceProbe, AzureServiceBusNamespaceProbe>();
        builder.Services.AddHostedService<ServiceBusTransportStartupValidationHostedService>();

        builder.Services.AddRebus(configure =>
        {
            if (string.Equals(transport, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                configure.Transport(t =>
                    t.UseInMemoryTransport(InMemNetwork, "modular-template"));
            }
            else if (string.Equals(transport, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
                {
                    throw new InvalidOperationException(
                        "Connection string 'ConnectionStrings:service-bus' is required when Messaging:Transport is AzureServiceBus.");
                }

                configure.Transport(t =>
                    t.UseAzureServiceBus(serviceBusConnectionString, "modular-template"));
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unsupported Messaging:Transport '{transport}'. Expected 'AzureServiceBus' or 'InMemory'.");
            }

            configure.Routing(routing =>
                routing.TypeBased().Map<DurableTransportEnvelope>("modular-template"));

            return configure;
        });

        builder.Services.AutoRegisterHandlersFromAssemblyOf<RebusDurableTransportHandler>();
        builder.Services.AddScoped<IOutboxTransport, RebusOutboxTransport>();
        builder.Services.AddScoped<IOutboxDispatcher, OutboxDispatcher>();
        builder.Services.AddScoped<IInboxProcessor, InboxProcessor>();
        builder.Services.AddHostedService<OutboxDispatcherBackgroundService>();
        builder.Services.AddHostedService<InboxProcessorBackgroundService>();

        return builder;
    }
}
