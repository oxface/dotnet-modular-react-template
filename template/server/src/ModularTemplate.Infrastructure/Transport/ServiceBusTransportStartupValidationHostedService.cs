using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ModularTemplate.Infrastructure.Transport;

public sealed class ServiceBusTransportStartupValidationHostedService(
    IConfiguration configuration,
    IHostEnvironment environment,
    IServiceBusNamespaceProbe namespaceProbe,
    ILogger<ServiceBusTransportStartupValidationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string transport = MessagingTransportConfiguration.ResolveTransport(configuration, environment.EnvironmentName);

        if (!string.Equals(transport, "AzureServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string? serviceBusConnectionString = configuration["ConnectionStrings:service-bus"];

        if (string.IsNullOrWhiteSpace(serviceBusConnectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'ConnectionStrings:service-bus' is required when Messaging:Transport is AzureServiceBus.");
        }

        try
        {
            await namespaceProbe.ProbeAsync(serviceBusConnectionString, cancellationToken);
            logger.LogInformation("Azure Service Bus startup transport check succeeded.");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Azure Service Bus startup transport check failed.",
                ex);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
