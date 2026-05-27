using Microsoft.Extensions.Configuration;

namespace ModularTemplate.Infrastructure.Transport;

internal static class MessagingTransportConfiguration
{
    public static string ResolveTransport(IConfiguration configuration, string environmentName)
    {
        string? configuredTransport = configuration["Messaging:Transport"]?.Trim();

        if (!string.IsNullOrWhiteSpace(configuredTransport))
        {
            return configuredTransport;
        }

        return string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase)
            ? "InMemory"
            : "AzureServiceBus";
    }
}
